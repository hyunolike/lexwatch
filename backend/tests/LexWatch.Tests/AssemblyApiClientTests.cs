using System.Net;
using LexWatch.Api.Radar;

namespace LexWatch.Tests;

public static class AssemblyFixtures
{
    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(LawFixtures.Dir)!, "assembly", name));

    public static AssemblyApiClient Client(Func<HttpRequestMessage, HttpResponseMessage> fn, string key = "") =>
        new(new HttpClient(new StubHandler(fn)), new AssemblyApiOptions(key));
}

public class AssemblyApiClientTests
{
    private static AssemblyBill One(string fixture)
    {
        var r = Assert.IsType<AssemblyResult<IReadOnlyList<AssemblyBill>>.Ok>(AssemblyApiClient.Parse(AssemblyFixtures.Read(fixture)));
        return Assert.Single(r.Value);
    }

    [Fact]
    public void Parses_recorded_keyless_list()
    {
        var r = Assert.IsType<AssemblyResult<IReadOnlyList<AssemblyBill>>.Ok>(
            AssemblyApiClient.Parse(AssemblyFixtures.Read("recorded_labor_keyless.json")));
        Assert.Equal(5, r.Value.Count);
        Assert.Empty(r.BadRows);
        var first = r.Value[0];
        Assert.Equal("2223132", first.BillNo);
        Assert.Equal("근로기준법 일부개정법률안", first.BillName);
        Assert.Equal(new DateOnly(2026, 11, 23), first.ProposedOn);
        Assert.Equal("이헌승의원 등 11인", first.Proposer);
        Assert.Null(first.Committee);
        Assert.StartsWith("https://likms.assembly.go.kr/bill/billDetail.do?billId=", first.DetailUrl);
        Assert.Equal(BillStages.Received, BillStages.From(first));
        Assert.Equal(BillStages.Committee, BillStages.From(r.Value[1]));
    }

    [Fact]
    public void Stage_from_recorded_rows()
    {
        // 위원장 대안은 이 API에 없고 원안이 대안반영폐기로 끝난다(design 4절).
        Assert.Equal(BillStages.Merged, BillStages.From(One("recorded_bill_closed.json")));
        var merged = One("recorded_bill_merged.json");
        Assert.Equal(BillStages.Merged, BillStages.From(merged));
        Assert.Equal(new DateOnly(2026, 5, 7), merged.ProcOn);
        Assert.Empty(Assert.IsType<AssemblyResult<IReadOnlyList<AssemblyBill>>.Ok>(
            AssemblyApiClient.Parse(AssemblyFixtures.Read("recorded_alternative_none.json"))).Value);
        // 법사위 소관 법안은 LAW_* 없이 위원회 처리일만 있다. 처리 결과를 지우면 본회의 대기다.
        var judiciary = One("recorded_bill_judiciary_passed.json");
        Assert.Equal(BillStages.Passed, BillStages.From(judiciary));
        Assert.Null(judiciary.JudiciarySubmitOn);
        Assert.Equal(BillStages.Plenary, BillStages.From(judiciary with { ProcResult = null, ProcOn = null }));
        var passed = One("recorded_bill_passed.json");
        Assert.Equal(BillStages.Passed, BillStages.From(passed));
        Assert.Equal(new DateOnly(2026, 9, 17), passed.ProcOn);
    }

    [Fact]
    public void Stage_order_for_middle_steps()
    {
        var b = One("recorded_bill_passed.json") with
        {
            ProcResult = null, ProcOn = null,
        };
        Assert.Equal(BillStages.Plenary, BillStages.From(b));
        Assert.Equal(BillStages.Judiciary, BillStages.From(b with { JudiciaryProcOn = null }));
        Assert.Equal(BillStages.Committee, BillStages.From(b with { JudiciaryProcOn = null, JudiciarySubmitOn = null }));
        Assert.Equal(BillStages.Received,
            BillStages.From(b with { JudiciaryProcOn = null, JudiciarySubmitOn = null, CommitteeOn = null }));
        Assert.Equal(BillStages.Passed, BillStages.From(b with { ProcResult = "수정가결" }));
        Assert.Equal(BillStages.Closed, BillStages.From(b with { ProcResult = "철회" }));
        Assert.Equal(BillStages.Closed, BillStages.From(b with { ProcResult = "폐기" }));
        Assert.Equal(BillStages.Merged, BillStages.From(b with { ProcResult = "수정안반영폐기" }));
        Assert.Equal(BillStages.Merged, BillStages.From(b with { ProcResult = null, CommitteeResult = "대안반영폐기" }));
    }

    [Fact]
    public void No_data_is_empty_ok()
    {
        var r = Assert.IsType<AssemblyResult<IReadOnlyList<AssemblyBill>>.Ok>(
            AssemblyApiClient.Parse(AssemblyFixtures.Read("recorded_bill_none.json")));
        Assert.Empty(r.Value);
    }

    [Theory]
    [InlineData("recorded_bad_key.json", "ASSEMBLY_API_ERROR_290")]
    public void Error_result_is_error(string fixture, string code)
    {
        var r = Assert.IsType<AssemblyResult<IReadOnlyList<AssemblyBill>>.Error>(AssemblyApiClient.Parse(AssemblyFixtures.Read(fixture)));
        Assert.Equal(code, r.Code);
    }

    [Theory]
    [InlineData("Bad Request.")]
    [InlineData("<html></html>")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{\"other\":1}")]
    public void Non_api_bodies_are_errors(string body)
    {
        var r = Assert.IsType<AssemblyResult<IReadOnlyList<AssemblyBill>>.Error>(AssemblyApiClient.Parse(body));
        Assert.Equal("ASSEMBLY_API_ERROR_BODY", r.Code);
    }

    [Fact]
    public void Unknown_info_code_is_error()
    {
        var r = Assert.IsType<AssemblyResult<IReadOnlyList<AssemblyBill>>.Error>(
            AssemblyApiClient.Parse("""{"RESULT":{"CODE":"INFO-300","MESSAGE":"x"}}"""));
        Assert.Equal("ASSEMBLY_API_INFO_300", r.Code);
    }

    [Fact]
    public void Row_missing_required_field_is_reported_not_thrown()
    {
        const string body = """
            {"nzmimeepazxkubdpn":[{"head":[{"list_total_count":2},{"RESULT":{"CODE":"INFO-000"}}]},
             {"row":[{"BILL_ID":"A","BILL_NO":"1","BILL_NAME":"근로기준법 일부개정법률안","PROPOSE_DT":"2026-11-01"},
                     {"BILL_ID":"B","BILL_NO":"2","BILL_NAME":"근로기준법 일부개정법률안","PROPOSE_DT":null}]}]}
            """;
        var r = Assert.IsType<AssemblyResult<IReadOnlyList<AssemblyBill>>.Ok>(AssemblyApiClient.Parse(body));
        Assert.Single(r.Value);
        Assert.Equal(["2"], r.BadRows);
    }

    [Fact]
    public async Task Sends_user_agent_and_page_size_and_hides_key()
    {
        HttpRequestMessage? seen = null;
        var keyless = AssemblyFixtures.Client(req => { seen = req; return LawFixtures.Json(AssemblyFixtures.Read("recorded_labor_keyless.json")); });
        await keyless.ListByLawNameAsync("근로기준법");
        Assert.NotEmpty(seen!.Headers.UserAgent);
        Assert.Contains("pSize=5", seen.RequestUri!.Query);
        Assert.DoesNotContain("KEY=", seen.RequestUri.Query);
        Assert.Contains("AGE=22", seen.RequestUri.Query);

        var keyed = AssemblyFixtures.Client(req => { seen = req; throw new HttpRequestException("https://x?KEY=SECRET failed"); }, "SECRET");
        var r = Assert.IsType<AssemblyResult<IReadOnlyList<AssemblyBill>>.Error>(await keyed.ListByLawNameAsync("근로기준법"));
        Assert.Contains("KEY=SECRET", seen!.RequestUri!.Query);
        Assert.Contains("pSize=100", seen.RequestUri.Query);
        Assert.Equal("ASSEMBLY_API_NETWORK", r.Code);
        Assert.DoesNotContain("SECRET", r.Message);
    }

    [Fact]
    public async Task Http_error_status_is_error()
    {
        var c = AssemblyFixtures.Client(_ => LawFixtures.Json("x", HttpStatusCode.ServiceUnavailable));
        var r = Assert.IsType<AssemblyResult<IReadOnlyList<AssemblyBill>>.Error>(await c.GetByBillNoAsync("2200500"));
        Assert.Equal("ASSEMBLY_API_HTTP_503", r.Code);
    }

    [Theory]
    [InlineData("근로기준법 일부개정법률안", "근로기준법", true)]
    [InlineData("근로기준법 전부개정법률안", "근로기준법", true)]
    [InlineData("근로기준법 폐지법률안", "근로기준법", true)]
    [InlineData("근로기준법 일부개정법률안(대안)", "근로기준법", true)]
    [InlineData("근로기준법일부개정법률안", "근로기준법", true)]
    [InlineData("근로기준법 법률안", "근로기준법", false)]
    [InlineData("근로기준법  일부개정법률안", "근로기준법", false)]
    [InlineData(" 은행법 일부개정법률안 ", "은행법", true)]
    [InlineData("한국은행법 일부개정법률안", "은행법", false)]
    [InlineData("한국산업은행법 일부개정법률안", "은행법", false)]
    [InlineData("근로기준법 시행에 관한 특별법안", "근로기준법", false)]
    [InlineData("중대재해 처벌 등에 관한 법률 일부개정법률안", "중대재해 처벌 등에 관한 법률", true)]
    [InlineData("근로기준법 일부개정법률안", "", false)]
    public void Bill_name_matches_exact_law(string bill, string law, bool expected) =>
        Assert.Equal(expected, BillNames.Matches(bill, law));
}
