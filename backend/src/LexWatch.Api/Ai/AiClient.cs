namespace LexWatch.Api.Ai;

public partial class AiClient
{
    // 계약 밖 오류 본문(422 {code, detail} 등)이 역직렬화에 실패해도 원문을 예외에 남겨 게이트웨이가 코드를 읽게 한다.
    partial void Initialize() => ReadResponseAsString = true;
}
