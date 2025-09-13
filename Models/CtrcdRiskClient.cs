using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
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
        /// <param name="http">HttpClient; create if null</param>
        public CtrcdRiskClient(string endpoint = "http://127.0.0.1:8000/predict", HttpClient? http = null)
        {
            _endpoint = endpoint?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(endpoint));
            _http = http ?? new HttpClient();
        }

        /// <summary>
        /// Safe prediction method. Shows a dialog if the service is not running or fails.
        /// </summary>
        public async Task<RiskResult?> PredictSafeAsync(Patient patient, double? threshold = null, CancellationToken ct = default)
        {
            if (patient == null)
                throw new ArgumentNullException(nameof(patient));

            if (!await HealthAsync(ct: ct))
            {
                await ShowAlertAsync("Model service is not running. Please start http://127.0.0.1:8000");
                return null;
            }

            try
            {
                return await PredictAsync(patient, threshold, ct);
            }
            catch (Exception ex)
            {
                await ShowAlertAsync($"Failed to call model: {ex.Message}");
                return null;
            }
        }

        public Task<RiskResult> PredictAsync(Patient patient, double? threshold = null, CancellationToken ct = default)
        {
            var payload = new
            {
                data = patient,
                threshold = threshold
            };
            return PostJsonAsync(payload, ct);
        }

        public Task<RiskResult> PredictAsync(Dictionary<string, object> patientData, double? threshold = null, CancellationToken ct = default)
        {
            if (patientData is null || patientData.Count == 0)
                throw new ArgumentException("patientData is empty.");

            var payload = new
            {
                data = patientData,
                threshold = threshold
            };
            return PostJsonAsync(payload, ct);
        }

        private async Task<RiskResult> PostJsonAsync(object payload, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(payload, _jsonOpts);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(_endpoint, content, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var respJson = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<RiskResult>(respJson, _jsonOpts)
                         ?? throw new InvalidOperationException("Empty response or invalid JSON.");
            return result;
        }

        /// <summary>
        /// Check if FastAPI service is healthy.
        /// </summary>
        public async Task<bool> HealthAsync(string healthUrl = "http://127.0.0.1:8000/health", CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.GetAsync(healthUrl, ct).ConfigureAwait(false);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private async Task ShowAlertAsync(string msg)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Notice",
                msg,
                ButtonEnum.Ok,
                Icon.Warning);
            await box.ShowAsync();
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
