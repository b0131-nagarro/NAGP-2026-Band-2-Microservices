using LeaveService.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LeaveService.Services;

public interface IEmployeeServiceClient
{
    Task<EmployeeBalanceInfo?> GetBalanceAsync(Guid employeeId, string leaveType, string bearerToken);
    Task<bool> DeductBalanceAsync(Guid employeeId, string leaveType, int days, string bearerToken);
}

public class EmployeeServiceClient(HttpClient httpClient, ILogger<EmployeeServiceClient> logger) : IEmployeeServiceClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<EmployeeBalanceInfo?> GetBalanceAsync(
        Guid employeeId, string leaveType, string bearerToken)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);

        var url = $"/api/employees/{employeeId}/balances?year={DateTime.UtcNow.Year}";

        try
        {
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("EmployeeService returned {Status}", response.StatusCode);
                return null;
            }

            var balances = await JsonSerializer.DeserializeAsync<List<BalanceDto>>(
                await response.Content.ReadAsStreamAsync(), JsonOpts);

            var match = balances?.FirstOrDefault(b =>
                string.Equals(b.LeaveType, leaveType, StringComparison.OrdinalIgnoreCase));

            return match is null ? null : new EmployeeBalanceInfo(match.RemainingDays, match.TotalAllocated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "balance call failed for {EmpId}", employeeId);
            return null;
        }
    }

    public async Task<bool> DeductBalanceAsync(
        Guid employeeId, string leaveType, int days, string bearerToken)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);

        var body = JsonSerializer.Serialize(new { EmployeeId = employeeId, LeaveType = leaveType, Days = days });
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PutAsync($"/api/employees/{employeeId}/balances/deduct", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "deduct failed for {EmpId}", employeeId);
            return false;
        }
    }

    private record BalanceDto(Guid EmployeeId, string LeaveType, int TotalAllocated, int UsedDays, int RemainingDays, int Year);
}

public record EmployeeBalanceInfo(int RemainingDays, int TotalAllocated);
