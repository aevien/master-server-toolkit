using MasterServerToolkit.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public class AnalyticsWebController : DashboardPageWebController
    {
        private const int DefaultPage = 0;
        private const int DefaultPageSize = 1000;
        private const int MaxPage = 100000;
        private const int MaxPageSize = 1000;

        public override void Initialize(HttpServerModule webServer)
        {
            base.Initialize(webServer);

            webServer.RegisterGetHandler("api/v1/analytics/search", OnSearchAnalyticsHttpRequestHandler, UseCredentials);
            webServer.RegisterGetHandler("api/v1/analytics", OnGetAnalyticsHttpRequestHandler, UseCredentials);
            webServer.RegisterGetHandler("api/v1/analytics/user", OnGetAnalyticsJsonByUserIdHttpRequestHandler, UseCredentials);
            webServer.RegisterGetHandler("api/v1/analytics/id", OnGetAnalyticsJsonByIdHttpRequestHandler, UseCredentials);
            webServer.RegisterGetHandler("api/v1/analytics/key", OnGetAnalyticsJsonByKeyHttpRequestHandler, UseCredentials);
            webServer.RegisterGetHandler("api/v1/analytics/timestamp", OnGetAnalyticsJsonByTimestampHttpRequestHandler, UseCredentials);
            webServer.RegisterGetHandler("api/v1/analytics/timestamp-range", OnGetAnalyticsJsonByTimestampRangeHttpRequestHandler, UseCredentials);
        }

        private MstJson ToJson(IEnumerable<IAnalyticsInfoData> analyticsData)
        {
            MstJson json = MstJson.CreateArray();

            if (analyticsData == null)
                return json;

            foreach (var item in analyticsData)
            {
                if (item == null)
                    continue;

                json.Add(item.ToJson());
            }

            return json;
        }

        private MstJson ToSearchJson(DatabaseEntriesInfo<IAnalyticsInfoData> analyticsData)
        {
            MstJson json = MstJson.CreateObject();
            json.AddField("total", analyticsData?.total ?? 0);
            json.AddField("filtered", analyticsData?.filtered ?? 0);
            json.AddField("entries", MstJson.CreateArray());

            if (analyticsData?.entries == null)
                return json;

            foreach (var item in analyticsData.entries)
            {
                if (item == null)
                    continue;

                json["entries"].Add(item.ToJson());
            }

            return json;
        }

        private bool TryReadPaging(HttpListenerRequest request, out int size, out int page, out IHttpResult error)
        {
            error = null;
            page = DefaultPage;

            if (!TryReadIntRange(request.QueryString[MstParamKeys.QS_PARAM_SIZE], MstParamKeys.QS_PARAM_SIZE, DefaultPageSize, 1, MaxPageSize, out size, out error))
                return false;

            if (!TryReadIntRange(request.QueryString[MstParamKeys.QS_PARAM_PAGE], MstParamKeys.QS_PARAM_PAGE, DefaultPage, 0, MaxPage, out page, out error))
                return false;

            return true;
        }

        private bool TryReadIntRange(
            string rawValue,
            string key,
            int defaultValue,
            int minValue,
            int maxValue,
            out int value,
            out IHttpResult error)
        {
            error = null;
            value = defaultValue;

            if (rawValue == null)
                return true;

            if (!int.TryParse(rawValue, out value))
            {
                error = new BadRequestJson($"Query parameter '{key}' must be an integer");
                return false;
            }

            if (value < minValue || value > maxValue)
            {
                error = new BadRequestJson($"Query parameter '{key}' must be between {minValue} and {maxValue}");
                return false;
            }

            return true;
        }

        private bool TryCreateSearchFilter(HttpListenerRequest request, out Dictionary<string, object> filter, out IHttpResult error)
        {
            filter = new Dictionary<string, object>();

            foreach (var key in request.QueryString.AllKeys.Where(s => !string.IsNullOrEmpty(s)))
                filter[key] = request.QueryString[key];

            if (!TryApplyIntRange(filter, MstParamKeys.QS_PARAM_PAGE, DefaultPage, 0, MaxPage, out error))
                return false;

            if (!TryApplyIntRange(filter, MstParamKeys.QS_PARAM_SIZE, DefaultPageSize, 1, MaxPageSize, out error))
                return false;

            if (!TryValidateOptionalDateTime(filter, MstParamKeys.QS_PARAM_TIMESTAMP, out error))
                return false;

            if (!TryValidateOptionalTimestampRange(filter, out error))
                return false;

            return true;
        }

        private bool TryApplyIntRange(
            Dictionary<string, object> filter,
            string key,
            int defaultValue,
            int minValue,
            int maxValue,
            out IHttpResult error)
        {
            error = null;

            if (!filter.TryGetValue(key, out var rawValue) || rawValue == null)
            {
                filter[key] = defaultValue.ToString();
                return true;
            }

            string value = rawValue.ToString();

            if (!int.TryParse(value, out int parsedValue))
            {
                error = new BadRequestJson($"Query parameter '{key}' must be an integer");
                return false;
            }

            if (parsedValue < minValue || parsedValue > maxValue)
            {
                error = new BadRequestJson($"Query parameter '{key}' must be between {minValue} and {maxValue}");
                return false;
            }

            filter[key] = parsedValue.ToString();
            return true;
        }

        private bool TryValidateOptionalDateTime(Dictionary<string, object> filter, string key, out IHttpResult error)
        {
            error = null;

            if (!filter.TryGetValue(key, out var rawValue) || string.IsNullOrWhiteSpace(rawValue?.ToString()))
                return true;

            if (TryParseUtcDateTime(rawValue.ToString(), out _))
                return true;

            error = new BadRequestJson($"Query parameter '{key}' must be a valid date/time value");
            return false;
        }

        private bool TryValidateOptionalTimestampRange(Dictionary<string, object> filter, out IHttpResult error)
        {
            error = null;
            string startValue = filter.TryGetValue(MstParamKeys.QS_PARAM_START_TIMESTAMP, out var rawStart)
                ? rawStart?.ToString()
                : null;
            string endValue = filter.TryGetValue(MstParamKeys.QS_PARAM_END_TIMESTAMP, out var rawEnd)
                ? rawEnd?.ToString()
                : null;
            bool hasStart = !string.IsNullOrWhiteSpace(startValue);
            bool hasEnd = !string.IsNullOrWhiteSpace(endValue);

            if (!hasStart && !hasEnd)
                return true;

            if (!hasStart || !hasEnd)
            {
                error = new BadRequestJson($"Query parameters '{MstParamKeys.QS_PARAM_START_TIMESTAMP}' and '{MstParamKeys.QS_PARAM_END_TIMESTAMP}' must be provided together");
                return false;
            }

            if (!TryParseUtcDateTime(startValue, out DateTime from))
            {
                error = new BadRequestJson($"Query parameter '{MstParamKeys.QS_PARAM_START_TIMESTAMP}' must be a valid date/time value");
                return false;
            }

            if (!TryParseUtcDateTime(endValue, out DateTime to))
            {
                error = new BadRequestJson($"Query parameter '{MstParamKeys.QS_PARAM_END_TIMESTAMP}' must be a valid date/time value");
                return false;
            }

            if (to < from)
            {
                error = new BadRequestJson($"Query parameter '{MstParamKeys.QS_PARAM_END_TIMESTAMP}' must be greater than or equal to '{MstParamKeys.QS_PARAM_START_TIMESTAMP}'");
                return false;
            }

            return true;
        }

        private bool TryReadRequiredQueryString(HttpListenerRequest request, string key, out string value, out IHttpResult error)
        {
            error = null;
            value = request.QueryString[key];

            if (string.IsNullOrWhiteSpace(value))
            {
                error = new BadRequestJson($"Query parameter '{key}' cannot be empty");
                return false;
            }

            value = value.Trim();
            return true;
        }

        private bool TryReadDateTime(string rawValue, string key, out DateTime value, out IHttpResult error)
        {
            error = null;

            if (TryParseUtcDateTime(rawValue, out value))
                return true;

            error = new BadRequestJson($"Query parameter '{key}' must be a valid date/time value");
            return false;
        }

        private static bool TryParseUtcDateTime(string rawValue,
            out DateTime value)
        {
            return DateTime.TryParse(rawValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces |
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out value);
        }

        private bool TryGetAnalyticsModule(out AnalyticsModule analyticsModule, out IHttpResult error)
        {
            error = null;
            analyticsModule = Server.GetModule<AnalyticsModule>();

            if (analyticsModule == null)
            {
                error = new NotFoundJson("Analytics module not found");
                return false;
            }

            if (analyticsModule.DatabaseAccessor == null)
            {
                error = new InternalServerErrorJson("Analytics database accessor not found");
                return false;
            }

            return true;
        }

        #region HANDLERS

        private async Task<IHttpResult> OnSearchAnalyticsHttpRequestHandler(HttpListenerRequest request)
        {
            if (!TryCreateSearchFilter(request, out var filter, out var error))
                return error;

            if (!TryGetAnalyticsModule(out var analyticsModule, out error))
                return error;

            var analyticsData = await analyticsModule.Search(filter);
            return new JsonResult(ToSearchJson(analyticsData));
        }

        private async Task<IHttpResult> OnGetAnalyticsHttpRequestHandler(HttpListenerRequest request)
        {
            if (!TryReadPaging(request, out int size, out int page, out var error))
                return error;

            if (!TryGetAnalyticsModule(out var analyticsModule, out error))
                return error;

            var analyticsData = await analyticsModule.GetAll(size, page);
            return new JsonResult(ToJson(analyticsData));
        }

        private async Task<IHttpResult> OnGetAnalyticsJsonByUserIdHttpRequestHandler(HttpListenerRequest request)
        {
            if (!TryReadPaging(request, out int size, out int page, out var error))
                return error;

            if (!TryReadRequiredQueryString(request, MstParamKeys.QS_PARAM_USER_ID, out string userId, out error))
                return error;

            if (!TryGetAnalyticsModule(out var analyticsModule, out error))
                return error;

            var analyticsData = await analyticsModule.GetByUserId(userId, size, page);
            return new JsonResult(ToJson(analyticsData));
        }

        private async Task<IHttpResult> OnGetAnalyticsJsonByIdHttpRequestHandler(HttpListenerRequest request)
        {
            if (!TryReadRequiredQueryString(request, MstParamKeys.QS_PARAM_ID, out string eventId, out var error))
                return error;

            if (!TryGetAnalyticsModule(out var analyticsModule, out error))
                return error;

            var analyticsData = await analyticsModule.GetById(eventId);

            if (analyticsData != null)
                return new JsonResult(ToJson(new List<IAnalyticsInfoData>() { analyticsData }));

            return new NotFoundJson("Analytics entry not found");
        }

        private async Task<IHttpResult> OnGetAnalyticsJsonByKeyHttpRequestHandler(HttpListenerRequest request)
        {
            if (!TryReadPaging(request, out int size, out int page, out var error))
                return error;

            if (!TryReadRequiredQueryString(request, MstParamKeys.QS_PARAM_KEY, out string eventKey, out error))
                return error;

            if (!TryGetAnalyticsModule(out var analyticsModule, out error))
                return error;

            var analyticsData = await analyticsModule.GetByKey(eventKey, size, page);
            return new JsonResult(ToJson(analyticsData));
        }

        private async Task<IHttpResult> OnGetAnalyticsJsonByTimestampHttpRequestHandler(HttpListenerRequest request)
        {
            if (!TryReadRequiredQueryString(request, MstParamKeys.QS_PARAM_TIMESTAMP, out string timestamp, out var error))
                return error;

            if (!TryGetAnalyticsModule(out var analyticsModule, out error))
                return error;

            if (!TryReadDateTime(timestamp, MstParamKeys.QS_PARAM_TIMESTAMP, out DateTime parsedTimestamp, out error))
                return error;

            var analyticsData = await analyticsModule.GetByTimestamp(parsedTimestamp);
            return new JsonResult(ToJson(analyticsData));
        }

        private async Task<IHttpResult> OnGetAnalyticsJsonByTimestampRangeHttpRequestHandler(HttpListenerRequest request)
        {
            if (!TryReadPaging(request, out int size, out int page, out var error))
                return error;

            if (!TryReadRequiredQueryString(request, MstParamKeys.QS_PARAM_START_TIMESTAMP, out string startTimestamp, out error))
                return error;

            if (!TryReadRequiredQueryString(request, MstParamKeys.QS_PARAM_END_TIMESTAMP, out string endTimestamp, out error))
                return error;

            if (!TryGetAnalyticsModule(out var analyticsModule, out error))
                return error;

            if (!TryReadDateTime(startTimestamp, MstParamKeys.QS_PARAM_START_TIMESTAMP, out DateTime from, out error))
                return error;

            if (!TryReadDateTime(endTimestamp, MstParamKeys.QS_PARAM_END_TIMESTAMP, out DateTime to, out error))
                return error;

            if (to < from)
                return new BadRequestJson($"Query parameter '{MstParamKeys.QS_PARAM_END_TIMESTAMP}' must be greater than or equal to '{MstParamKeys.QS_PARAM_START_TIMESTAMP}'");

            var analyticsData = await analyticsModule
                .GetByTimestampRange(from, to, size, page);
            return new JsonResult(ToJson(analyticsData));
        }

        #endregion
    }
}
