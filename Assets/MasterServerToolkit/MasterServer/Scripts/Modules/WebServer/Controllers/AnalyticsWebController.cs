using MasterServerToolkit.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AnalyticsWebController : WebController
    {
        [SerializeField]
        private TextAsset analyticsDashboardPage;

        private string analyticsDashboardPageHtml;

        public override void Initialize(WebServerModule webServer)
        {
            base.Initialize(webServer);

            analyticsDashboardPageHtml = analyticsDashboardPage.text;

            webServer.RegisterGetHandler("analytics", OnGetAnalyticsHttpRequestHandler, UseCredentials);
            webServer.RegisterGetHandler("analytics/panel", OnGetAnalyticsPanelHttpRequestHandler, UseCredentials);
            webServer.RegisterGetHandler("analytics/user", OnGetAnalyticsJsonByUserIdHttpRequestHandler, UseCredentials);
            webServer.RegisterGetHandler("analytics/id", OnGetAnalyticsJsonByIdHttpRequestHandler, UseCredentials);
            webServer.RegisterGetHandler("analytics/key", OnGetAnalyticsJsonByKeyHttpRequestHandler, UseCredentials);
            webServer.RegisterGetHandler("analytics/timestamp", OnGetAnalyticsJsonByTimestampHttpRequestHandler, UseCredentials);
            webServer.RegisterGetHandler("analytics/timestamp-range", OnGetAnalyticsJsonByTimestampRangeHttpRequestHandler, UseCredentials);
        }

        private MstJson ToJson(IEnumerable<IAnalyticsInfoData> analyticsData)
        {
            MstJson json = MstJson.EmptyArray;

            foreach (var item in analyticsData)
            {
                json.Add(item.ToJson());
            }

            return json;
        }

        #region HANDLERS

        private async Task<IHttpResult> OnGetAnalyticsHttpRequestHandler(HttpListenerRequest request)
        {
            int size = 1000;
            int page = 0;

            if (int.TryParse(request.QueryString["s"], out int sizeResult))
                size = sizeResult;

            if (int.TryParse(request.QueryString["p"], out int pageResult))
                page = pageResult;

            var analyticsModule = MasterServer.GetModule<AnalyticsModule>();

            if (analyticsModule == null)
            {
                return new NotFound("Analytics module not found");
            }
            else
            {
                var analyticsData = await analyticsModule.GetAll(size, page);
                return new JsonResult(ToJson(analyticsData));
            }
        }

        private Task<IHttpResult> OnGetAnalyticsPanelHttpRequestHandler(HttpListenerRequest request)
        {
            return Task.FromResult<IHttpResult>(new HtmlResult(analyticsDashboardPageHtml));
        }

        private async Task<IHttpResult> OnGetAnalyticsJsonByUserIdHttpRequestHandler(HttpListenerRequest request)
        {
            string userId = request.QueryString["d"];
            int size = 1000;
            int page = 0;

            if (int.TryParse(request.QueryString["s"], out int sizeResult))
                size = sizeResult;

            if (int.TryParse(request.QueryString["p"], out int pageResult))
                page = pageResult;

            var analyticsModule = MasterServer.GetModule<AnalyticsModule>();

            if (analyticsModule == null)
            {
                return new NotFound("Analytics module not found");
            }
            else if (string.IsNullOrEmpty(userId))
            {
                return new BadRequest("User id cannot be empty");
            }
            else
            {
                var analyticsData = await analyticsModule.GetByUserId(userId, size, page);
                return new JsonResult(ToJson(analyticsData));
            }
        }

        private async Task<IHttpResult> OnGetAnalyticsJsonByIdHttpRequestHandler(HttpListenerRequest request)
        {
            string eventId = request.QueryString["d"];

            var analyticsModule = MasterServer.GetModule<AnalyticsModule>();

            if (analyticsModule == null)
            {
                return new NotFound("Analytics module not found");
            }
            else if (string.IsNullOrEmpty(eventId))
            {
                return new BadRequest("Event id cannot be empty");
            }
            else
            {
                var analyticsData = await analyticsModule.GetById(eventId);

                if (analyticsData != null)
                {
                    return new JsonResult(ToJson(new List<IAnalyticsInfoData>() { analyticsData }));
                }
                else
                {
                    return new NotFound("Analytics entry not found");
                }
            }
        }

        private async Task<IHttpResult> OnGetAnalyticsJsonByKeyHttpRequestHandler(HttpListenerRequest request)
        {
            string eventKey = request.QueryString["d"];
            int size = 1000;
            int page = 0;

            if (int.TryParse(request.QueryString["s"], out int sizeResult))
                size = sizeResult;

            if (int.TryParse(request.QueryString["p"], out int pageResult))
                page = pageResult;

            var analyticsModule = MasterServer.GetModule<AnalyticsModule>();

            if (analyticsModule == null)
            {
                return new NotFound("Analytics module not found");
            }
            else if (string.IsNullOrEmpty(eventKey))
            {
                return new BadRequest("Event key cannot be empty");
            }
            else
            {
                var analyticsData = await analyticsModule.GetByKey(eventKey, size, page);
                return new JsonResult(ToJson(analyticsData));
            }
        }

        private async Task<IHttpResult> OnGetAnalyticsJsonByTimestampHttpRequestHandler(HttpListenerRequest request)
        {
            string timestamp = request.QueryString["d"];

            var analyticsModule = MasterServer.GetModule<AnalyticsModule>();

            if (analyticsModule == null)
            {
                return new NotFound("Analytics module not found");
            }
            else if (string.IsNullOrEmpty(timestamp))
            {
                return new BadRequest("Event timestamp cannot be empty");
            }
            else
            {
                var analyticsData = await analyticsModule.GetByTimestamp(DateTime.Parse(timestamp));
                return new JsonResult(ToJson(analyticsData));
            }
        }

        private async Task<IHttpResult> OnGetAnalyticsJsonByTimestampRangeHttpRequestHandler(HttpListenerRequest request)
        {
            string timestampRange = request.QueryString["d"];
            int size = 1000;
            int page = 0;

            if (int.TryParse(request.QueryString["s"], out int sizeResult))
                size = sizeResult;

            if (int.TryParse(request.QueryString["p"], out int pageResult))
                page = pageResult;

            var analyticsModule = MasterServer.GetModule<AnalyticsModule>();

            if (analyticsModule == null)
            {
                return new NotFound("Analytics module not found");
            }
            else if (string.IsNullOrEmpty(timestampRange))
            {
                return new BadRequest("Event timestamp range cannot be empty");
            }
            else
            {
                var timestampRanges = timestampRange.Split(',');

                if (timestampRanges.Length == 2)
                {
                    var analyticsData = await analyticsModule
                        .GetByTimestampRange(DateTime.Parse(timestampRanges[0]), DateTime.Parse(timestampRanges[1]), size, page);
                    return new JsonResult(ToJson(analyticsData));
                }
                else
                {
                    return new BadRequest("Invalid timestamp range format");
                }
            }
        }

        #endregion
    }
}