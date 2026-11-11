using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SchoolAdmission.Tests;

public class AdmissionApiTests
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:8080")
    };
    private static string Name(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
    private static string Student(int number) => $"20{number:D15}";

    private static async Task<string> CreateClassAsync(int capacity)
    {
        var response = await Client.PostAsJsonAsync("/classes", new { name = Name("Automated test"), totalSeats = capacity });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }
    private static async Task<HttpResponseMessage> HoldAsync(string classId, string studentId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        request.Headers.TryAddWithoutValidation("X-Student-Id", studentId);
        return await Client.SendAsync(request);
    }
    private static async Task<HttpResponseMessage> ChangeHoldAsync(string holdId, string studentId, HttpMethod method, string suffix = "")
    {
        using var request = new HttpRequestMessage(method, $"/holds/{holdId}{suffix}");
        request.Headers.TryAddWithoutValidation("X-Student-Id", studentId);
        return await Client.SendAsync(request);
    }
    private static async Task<int> GetCountAsync(string classId, string field)
    {
        using var response = await Client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty(field).GetInt32();
    }

    [Fact]
    public async Task CreatingAClassReturnsMatchingSeatCounts()
    {
        string id = await CreateClassAsync(9);
        Assert.Equal(9, await GetCountAsync(id, "totalSeats"));
        Assert.Equal(9, await GetCountAsync(id, "availableSeats"));
        Assert.Equal(0, await GetCountAsync(id, "confirmedSeats"));
    }

    [Fact]
    public async Task InvalidStudentIdIsRejectedWith400()
    {
        string id = await CreateClassAsync(1);
        using var response = await HoldAsync(id, "123");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("1234567890123456")]
    [InlineData("123456789012345678")]
    [InlineData("1234567890123456A")]
    [InlineData("1234567890")]
    public async Task StudentIdMustHaveExactly17Digits(string invalidId)
    {
        string id = await CreateClassAsync(1);
        using var create = await HoldAsync(id, invalidId);
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(1, await GetCountAsync(id, "availableSeats"));
    }

    [Fact]
    public async Task ClassListAndStudentApplicationReferenceTheSameClass()
    {
        string classId = await CreateClassAsync(2);
        string student = Student(44);
        using var create = await HoldAsync(classId, student);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var classesResponse = await Client.GetAsync("/classes");
        Assert.Equal(HttpStatusCode.OK, classesResponse.StatusCode);
        using var classDoc = JsonDocument.Parse(await classesResponse.Content.ReadAsStringAsync());
        bool listed = classDoc.RootElement.EnumerateArray()
            .Any(c => c.GetProperty("id").GetString() == classId);
        Assert.True(listed, "The class must remain present in GET /classes.");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        request.Headers.Add("X-Student-Id", student);
        using var holdsResponse = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, holdsResponse.StatusCode);
        using var holdDoc = JsonDocument.Parse(await holdsResponse.Content.ReadAsStringAsync());
        bool found = holdDoc.RootElement.EnumerateArray()
            .Any(h => h.GetProperty("classId").GetString() == classId);
        Assert.True(found, "The student's application must reference the same class.");
    }

    [Fact]
    public async Task SameStudentCannotHoldTwoSeatsInSameClass()
    {
        string id = await CreateClassAsync(3);
        string student = Student(7);
        using var first = await HoldAsync(id, student);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var second = await HoldAsync(id, student);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, await GetCountAsync(id, "heldSeats"));
    }

    [Fact]
    public async Task RepeatedConfirmationDoesNotDoubleCountAndRejectsOtherStudent()
    {
        string id = await CreateClassAsync(1);
        string owner = Student(17);
        using var create = await HoldAsync(id, owner);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        string holdId = doc.RootElement.GetProperty("holdId").GetString()!;

        using var attacker = await ChangeHoldAsync(holdId, Student(18), HttpMethod.Post, "/confirm");
        Assert.Equal(HttpStatusCode.Forbidden, attacker.StatusCode);
        using var first = await ChangeHoldAsync(holdId, owner, HttpMethod.Post, "/confirm");
        using var second = await ChangeHoldAsync(holdId, owner, HttpMethod.Post, "/confirm");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, await GetCountAsync(id, "confirmedSeats"));
        Assert.Equal(0, await GetCountAsync(id, "heldSeats"));
    }

    [Fact]
    public async Task CancellingAReservationReturnsTheSeat()
    {
        string id = await CreateClassAsync(1);
        string owner = Student(31);
        using var create = await HoldAsync(id, owner);
        using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        string holdId = doc.RootElement.GetProperty("holdId").GetString()!;
        Assert.Equal(0, await GetCountAsync(id, "availableSeats"));
        using var cancel = await ChangeHoldAsync(holdId, owner, HttpMethod.Delete);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(1, await GetCountAsync(id, "availableSeats"));
    }

    [Fact]
    public async Task ConcurrentRequestsNeverExceedCapacity()
    {
        string id = await CreateClassAsync(3);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(100, 50).Select(async n =>
        {
            await gate.Task;
            using var response = await HoldAsync(id, Student(n));
            return response.StatusCode;
        }).ToArray();
        gate.SetResult(true);
        var statuses = await Task.WhenAll(tasks);
        Assert.Equal(3, statuses.Count(x => x == HttpStatusCode.Created));
        Assert.Equal(47, statuses.Count(x => x == HttpStatusCode.Conflict));
        Assert.Equal(3, await GetCountAsync(id, "heldSeats"));
        Assert.Equal(0, await GetCountAsync(id, "availableSeats"));
    }
}
