using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ClinicalApplications.Models
{
    public sealed class CtrcdRiskClient
    {
        private readonly HttpClient _http;
        private readonly string _endpoint;
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        /// <param name="endpoint">default http://127.0.0.1:8000/predict</param>
        /// <param name="http">HttpClient；create if null</param>
        public CtrcdRiskClient(string endpoint = "http://127.0.0.1:8000/predict", HttpClient? http = null)
        {
            _endpoint = endpoint?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(endpoint));
            _http = http ?? new HttpClient();
        }
        public async Task<RiskResult> PredictAsync(
            Dictionary<string, object> patientData,
            double? threshold = null,
            CancellationToken ct = default)
        {
            if (patientData is null || patientData.Count == 0)
                throw new ArgumentException("patientData is empty.");

            var payload = new
            {
                data = patientData,
                threshold = threshold
            };

            var json = JsonSerializer.Serialize(payload, _jsonOpts);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(_endpoint, content, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var respJson = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<RiskResult>(respJson, _jsonOpts)
                         ?? throw new InvalidOperationException("Empty response or invalid JSON.");
            return result;
        }

        public async Task<bool> HealthAsync(string healthUrl = "http://127.0.0.1:8000/health", CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.GetAsync(healthUrl, ct).ConfigureAwait(false);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }

    public sealed class RiskResult
    {
        [JsonPropertyName("prob")]
        public double Prob { get; set; }

        [JsonPropertyName("pred")]
        public int Pred { get; set; }

        [JsonPropertyName("threshold")]
        public double Threshold { get; set; }

        [JsonPropertyName("feature_count")]
        public int FeatureCount { get; set; }

        [JsonPropertyName("echo")]
        public Dictionary<string, object>? Echo { get; set; }
    }
}
