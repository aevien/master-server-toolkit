using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class SmtpMailer : Mailer
    {
        private List<Exception> sendMailExceptions;
        private Logging.Logger logger;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        protected SmtpClient smtpClient;
        private readonly SemaphoreSlim smtpSendLock = new SemaphoreSlim(1, 1);
        private readonly object smtpClientSync = new object();
        private readonly CancellationTokenSource lifecycleCancellation = new CancellationTokenSource();
        private SmtpSendOperation activeSendOperation;
        private int isDestroyed;

        private const int SendCancellationGracePeriodMilliseconds = 2000;
#endif

        [Header("E-mail settings")]
        [Tooltip("SMTP server host name or IP address. The -mstSmtpHost argument overrides this value at startup.")]
        public string smtpHost = "smtp.mail.com";
        [Tooltip("Account name used to authenticate with the SMTP server. The -mstSmtpUsername argument overrides this value.")]
        public string smtpUsername = "username@mail.com";
        [Tooltip("Password used to authenticate with the SMTP server. The -mstSmtpPassword argument overrides this value.")]
        public string smtpPassword = "password";
        [Tooltip("SMTP server TCP port used by System.Net.Mail. Enable SSL uses STARTTLS, commonly on port 587; implicit SMTPS on port 465 is not supported by this client. The -mstSmtpPort argument overrides this value.")]
        public int smtpPort = 587;
        [Tooltip("Enables STARTTLS after the initial SMTP connection. Commonly combine this with port 587. This does not enable implicit SMTPS on port 465. The -mstSmtpEnableSSL argument overrides this value.")]
        public bool enableSsl = true;
        [Tooltip("Maximum duration in seconds allowed for an SMTP send operation. Use a positive value; the -mstSmtpTimeout argument overrides this value.")]
        public int timeoutInSeconds = 60;
        [Tooltip("Email address written to the From header. The SMTP provider may require it to match the authenticated account. The -mstSmtpMailFrom argument overrides this value.")]
        public string mailFrom = "yourgame@mail.com";
        [Tooltip("Human-readable sender name written to outgoing messages. The -mstSmtpSenderDisplayName argument overrides this value.")]
        public string senderDisplayName = "Awesome Game";

        [Header("E-mail template"), SerializeField, Tooltip("Optional HTML body template used for outgoing mail. Leave None to send the body supplied by the caller without wrapping it in a template.")]
        protected TextAsset emailBodyTemplate;

        string htmlTemplate = string.Empty;

        protected virtual void Awake()
        {
            if (emailBodyTemplate)
                htmlTemplate = emailBodyTemplate.text;

            logger = Mst.Create.Logger(typeof(SmtpMailer).Name);
            sendMailExceptions = new List<Exception>();

            smtpHost = Mst.Args.AsString(Mst.Args.Names.SmtpHost, smtpHost);
            smtpUsername = Mst.Args.AsString(Mst.Args.Names.SmtpUsername, smtpUsername);
            smtpPassword = Mst.Args.AsString(Mst.Args.Names.SmtpPassword, smtpPassword);
            smtpPort = Mst.Args.AsInt(Mst.Args.Names.SmtpPort, smtpPort);
            enableSsl = Mst.Args.AsBool(Mst.Args.Names.SmtpEnableSSL, enableSsl);
            timeoutInSeconds = Mst.Args.AsInt(Mst.Args.Names.SmtpTimeout, timeoutInSeconds);
            mailFrom = Mst.Args.AsString(Mst.Args.Names.SmtpMailFrom, mailFrom);
            senderDisplayName = Mst.Args.AsString(Mst.Args.Names.SmtpSenderDisplayName, senderDisplayName);

            SetupSmtpClient();
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void Update()
        {
            List<Exception> exceptionsToLog;

            lock (sendMailExceptions)
            {
                if (sendMailExceptions.Count == 0)
                    return;

                exceptionsToLog = new List<Exception>(sendMailExceptions);
                sendMailExceptions.Clear();
            }

            foreach (Exception exception in exceptionsToLog)
                logger.Error(exception);
        }

        protected virtual void OnDestroy()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            if (Interlocked.Exchange(ref isDestroyed, 1) != 0)
                return;

            lifecycleCancellation.Cancel();

            SmtpClient idleClient;
            SmtpSendOperation activeOperation;

            lock (smtpClientSync)
            {
                activeOperation = activeSendOperation;
                activeSendOperation = null;
                idleClient = activeOperation == null ? smtpClient : null;
                smtpClient = null;
            }

            if (activeOperation != null)
            {
                CancelAndScheduleOperationCleanup(activeOperation);
            }
            else
            {
                DisposeIdleSmtpClient(idleClient);
            }
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void SetupSmtpClient()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            SmtpClient newClient = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = enableSsl,
                Timeout = timeoutInSeconds * 1000
            };

            newClient.SendCompleted += OnSendCompleted;

            SmtpClient previousClient;

            lock (smtpClientSync)
            {
                if (Volatile.Read(ref isDestroyed) != 0)
                {
                    DisposeIdleSmtpClient(newClient);
                    return;
                }

                previousClient = smtpClient;
                smtpClient = newClient;
            }

            DisposeIdleSmtpClient(previousClient);
#endif
        }

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        private SmtpClient GetOrCreateSmtpClient()
        {
            lock (smtpClientSync)
            {
                if (Volatile.Read(ref isDestroyed) != 0)
                    throw new ObjectDisposedException(nameof(SmtpMailer));

                if (smtpClient == null)
                    SetupSmtpClient();

                return smtpClient ?? throw new ObjectDisposedException(nameof(SmtpMailer));
            }
        }

        private SmtpSendOperation BeginSend(MailMessage mailMessage)
        {
            lock (smtpClientSync)
            {
                if (Volatile.Read(ref isDestroyed) != 0)
                    throw new ObjectDisposedException(nameof(SmtpMailer));

                SmtpClient client = GetOrCreateSmtpClient();
                Task sendTask = client.SendMailAsync(mailMessage);
                var operation = new SmtpSendOperation(client, mailMessage, sendTask);
                activeSendOperation = operation;
                return operation;
            }
        }

        private void CompleteActiveSend(SmtpSendOperation operation)
        {
            lock (smtpClientSync)
            {
                if (ReferenceEquals(activeSendOperation, operation))
                    activeSendOperation = null;
            }
        }

        private void DetachActiveSend(SmtpSendOperation operation)
        {
            lock (smtpClientSync)
            {
                if (ReferenceEquals(activeSendOperation, operation))
                    activeSendOperation = null;

                if (ReferenceEquals(smtpClient, operation.Client))
                    smtpClient = null;
            }
        }

        private void CancelAndScheduleOperationCleanup(SmtpSendOperation operation)
        {
            DetachActiveSend(operation);

            if (!operation.TryScheduleCleanup())
                return;

            Task cancellationTask = RequestSendCancellation(operation.Client);
            ScheduleOperationCleanup(operation, cancellationTask);
        }

        private Task RequestSendCancellation(SmtpClient client)
        {
            return Task.Run(() =>
            {
                try
                {
                    client.SendAsyncCancel();
                }
                catch (ObjectDisposedException)
                {
                    // The client was already retired by another lifecycle path.
                }
                catch (InvalidOperationException)
                {
                    // The send completed before cancellation reached the client.
                }
                catch (Exception exception)
                {
                    QueueSendException(exception);
                }
            });
        }

        private void ScheduleOperationCleanup(
            SmtpSendOperation operation,
            Task cancellationTask)
        {
            Task sendObserver = ObserveTaskAsync(operation.SendTask);
            Task cancellationObserver = ObserveTaskAsync(cancellationTask);

            Task.WhenAll(sendObserver, cancellationObserver).ContinueWith(
                _ =>
                {
                    Exception cleanupException = null;

                    try
                    {
                        operation.Message.Dispose();
                    }
                    catch (Exception exception)
                    {
                        cleanupException = exception;
                    }

                    try
                    {
                        DisposeIdleSmtpClient(operation.Client);
                    }
                    catch (Exception exception)
                    {
                        cleanupException = cleanupException == null
                            ? exception
                            : new AggregateException(cleanupException, exception);
                    }

                    if (cleanupException != null)
                        QueueSendException(cleanupException);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static async Task ObserveTaskAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // The owning SendMailAsync call reports the primary timeout or cancellation.
            }
        }

        private void DisposeIdleSmtpClient(SmtpClient client)
        {
            if (client == null)
                return;

            client.SendCompleted -= OnSendCompleted;
            client.Dispose();
        }

        private void OnSendCompleted(object sender, AsyncCompletedEventArgs args)
        {
            if (args.Error == null && !args.Cancelled && Volatile.Read(ref isDestroyed) == 0)
                logger.Debug("Email is successfully sent");
        }

        private void QueueSendException(Exception exception)
        {
            if (Volatile.Read(ref isDestroyed) != 0)
                return;

            lock (sendMailExceptions)
                sendMailExceptions.Add(exception);
        }

        private sealed class SmtpSendOperation
        {
            private int cleanupScheduled;

            public SmtpSendOperation(
                SmtpClient client,
                MailMessage message,
                Task sendTask)
            {
                Client = client;
                Message = message;
                SendTask = sendTask;
            }

            public SmtpClient Client { get; }
            public MailMessage Message { get; }
            public Task SendTask { get; }
            public bool IsCleanupScheduled => Volatile.Read(ref cleanupScheduled) != 0;

            public bool TryScheduleCleanup()
            {
                return Interlocked.Exchange(ref cleanupScheduled, 1) == 0;
            }
        }
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="to"></param>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        public override async Task<bool> SendMailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

            bool sendLockTaken = false;
            MailMessage mailMessage = null;
            SmtpSendOperation sendOperation = null;

            using (CancellationTokenSource operationCancellation =
                   CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken,
                       lifecycleCancellation.Token))
            using (CancellationTokenSource deadlineCancellation =
                   CancellationTokenSource.CreateLinkedTokenSource(
                       operationCancellation.Token))
            {
                int timeoutSeconds = Math.Max(1, timeoutInSeconds);
                deadlineCancellation.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                try
                {
                    await smtpSendLock.WaitAsync(deadlineCancellation.Token);
                    sendLockTaken = true;

                    string messageBody = body;
                    string generatedMessageBody = htmlTemplate;

                    if (!string.IsNullOrEmpty(htmlTemplate))
                    {
                        generatedMessageBody = generatedMessageBody.Replace("#{MESSAGE_SUBJECT}", subject);
                        generatedMessageBody = generatedMessageBody.Replace("#{MESSAGE_BODY}", body);
                        generatedMessageBody = generatedMessageBody.Replace("#{MESSAGE_YEAR}", DateTime.Now.Year.ToString());

                        messageBody = generatedMessageBody;
                    }

                    mailMessage = new MailMessage
                    {
                        From = new MailAddress(mailFrom, senderDisplayName),
                        Subject = subject,
                        Body = messageBody,
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(to);

                    deadlineCancellation.Token.ThrowIfCancellationRequested();
                    sendOperation = BeginSend(mailMessage);
                    Task sendTask = sendOperation.SendTask;
                    Task deadlineTask = Task.Delay(
                        System.Threading.Timeout.Infinite,
                        deadlineCancellation.Token);
                    Task completedTask = await Task.WhenAny(sendTask, deadlineTask);

                    if (completedTask == sendTask)
                    {
                        await sendTask;
                        deadlineCancellation.Token.ThrowIfCancellationRequested();
                    }
                    else
                    {
                        CancelAndScheduleOperationCleanup(sendOperation);

                        Task cancellationGraceTask = Task.Delay(
                            SendCancellationGracePeriodMilliseconds);
                        await Task.WhenAny(
                            sendTask,
                            cancellationGraceTask);

                        operationCancellation.Token.ThrowIfCancellationRequested();
                        throw new TimeoutException(
                            $"SMTP operation exceeded the configured timeout of " +
                            $"{timeoutSeconds} seconds");
                    }

                    return true;
                }
                catch (OperationCanceledException)
                {
                    if (operationCancellation.IsCancellationRequested)
                        throw;

                    QueueSendException(
                        new TimeoutException(
                            $"SMTP operation exceeded the configured timeout of " +
                            $"{timeoutSeconds} seconds"));
                    return false;
                }
                catch (Exception e)
                {
                    operationCancellation.Token.ThrowIfCancellationRequested();
                    QueueSendException(e);

                    return false;
                }
                finally
                {
                    if (sendOperation != null)
                        CompleteActiveSend(sendOperation);

                    if (sendOperation == null || !sendOperation.IsCleanupScheduled)
                        mailMessage?.Dispose();

                    if (sendLockTaken)
                        smtpSendLock.Release();
                }
            }
#else
            return true;
#endif
        }
    }
}
