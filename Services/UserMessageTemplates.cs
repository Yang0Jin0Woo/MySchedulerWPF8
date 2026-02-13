namespace MyScheduler.Services;

public static class UserMessageTemplates
{
    public static string BuildDbHint(string action)
    {
        return
            $"{action} 중 문제가 발생했습니다.\n\n" +
            "확인해야 할 항목:\n" +
            "1) SQL Server가 실행 중인지\n" +
            "2) appsettings.json 연결 문자열이 올바른지\n" +
            "3) DB 권한 문제가 없는지\n\n" +
            "조치 방법:\n" +
            "- Windows 서비스에서 'SQL Server(SQLEXPRESS)' 실행 확인\n" +
            "- SSMS로 localhost\\SQLEXPRESS 접속 테스트\n" +
            "- appsettings.json의 ConnectionStrings:Default 확인";
    }
}
