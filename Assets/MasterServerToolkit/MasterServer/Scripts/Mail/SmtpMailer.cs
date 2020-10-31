using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
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
#endif

        [Header("E-mail settings")]
        public string smtpHost = "smtp.mail.com";
        public string smtpUsername = "username@mail.com";
        public string smtpPassword = "password";
        public int smtpPort = 587;
        public bool enableSsl = true;
        public int timeoutInSeconds = 60;
        public string mailFrom = "yourgame@mail.com";
        public string senderDisplayName = "Awesome Game";

        [Header("E-mail template"), SerializeField]
        protected TextAsset emailBodyTemplate;

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(typeof(SmtpMailer).Name);
            sendMailExceptions = new List<Exception>();
            SetupSmtpClient();
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void Update()
        {
            // Log errors for any exceptions that might have occured
            // when sending mail
            if (sendMailExceptions.Count > 0)
            {
                lock (sendMailExceptions)
                {
                    foreach (var exception in sendMailExceptions)
                    {
                        logger.Error(exception);
                    }

                    sendMailExceptions.Clear();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void SetupSmtpClient()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

            smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUsername, smtpPassword) as ICredentialsByHost,
                EnableSsl = enableSsl,
                Timeout = timeoutInSeconds * 1000
            };

            smtpClient.SendCompleted += (sender, args) =>
            {
                if (args.Error != null)
                {
                    lock (sendMailExceptions)
                    {
                        sendMailExceptions.Add(args.Error);
                    }
                }
                else if (args.Cancelled)
                {
                    lock (sendMailExceptions)
                    {
                        sendMailExceptions.Add(new Exception("Email sending cancelled!"));
                    }
                }
                else
                {
                    logger.Debug("Email is successfully sent");
                }
            };

            ServicePointManager.ServerCertificateValidationCallback = delegate (object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) { return true; };
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="to"></param>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        public override async Task<bool> SendMailAsync(string to, string subject, string body)
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

            try
            {
                string messageBody = body;

                if (emailBodyTemplate)
                {
                    string generatedMessageBody = emailBodyTemplate.text;

                    generatedMessageBody = generatedMessageBody.Replace("#{MESSAGE_SUBJECT}", subject);
                    generatedMessageBody = generatedMessageBody.Replace("#{MESSAGE_BODY}", body);
                    generatedMessageBody = generatedMessageBody.Replace("#{MESSAGE_YEAR}", DateTime.Now.Year.ToString());

                    messageBody = generatedMessageBody;
                }

                // Create the mail message (from, to, subject, body)
                MailMessage mailMessage = new MailMessage
                {
                    From = new MailAddress(mailFrom, senderDisplayName),
                    Subject = subject,
                    Body = messageBody,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(to);

                // send the mail
                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception e)
            {
                lock (sendMailExceptions)
                {
                    sendMailExceptions.Add(e);
                }

                return false;
            }
#else
            return true;
#endif
        }

        //public virtual async Task<bool> SendEmailConfirmation
    }
}