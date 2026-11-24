namespace LexWatch.Api.Radar;

/// <summary>실무 그룹별로 지켜볼 법률 이름의 시드(bill-radar BR-01). 마이그레이션과 데모 시더가 같은 값을 쓴다.</summary>
public static class BillWatch
{
    public static readonly IReadOnlyDictionary<string, string[]> Defaults = new Dictionary<string, string[]>
    {
        ["labor"] =
        [
            "근로기준법", "최저임금법", "산업안전보건법", "중대재해 처벌 등에 관한 법률", "근로자퇴직급여 보장법",
            "노동조합 및 노동관계조정법",
        ],
        ["privacy"] =
        [
            "개인정보 보호법", "정보통신망 이용촉진 및 정보보호 등에 관한 법률", "위치정보의 보호 및 이용 등에 관한 법률",
            "신용정보의 이용 및 보호에 관한 법률",
        ],
        ["finance"] =
        [
            "금융소비자 보호에 관한 법률", "자본시장과 금융투자업에 관한 법률", "전자금융거래법", "은행법", "보험업법",
        ],
    };
}
