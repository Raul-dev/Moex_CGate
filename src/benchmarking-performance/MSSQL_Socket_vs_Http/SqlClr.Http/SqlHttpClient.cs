using System;
using System.Data.SqlTypes;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.SqlServer.Server;
using SqlPerf;

namespace SqlClr
{
    /// <summary>
    /// SQL CLR-функция: запрос тех же 5 КБ «котировок позиции» по HTTP GET
    /// (Kestrel-сервис PositionHttpService) для сравнения с TCP-вариантом.
    /// Приложение рассчитано на PERMISSION_SET = UNSAFE.
    /// </summary>
    public static class SqlHttpClient
    {
        static SqlHttpClient()
        {
            // HTTP/1.1 keep-alive: пул соединений HttpWebRequest, лимит зеркалирует число параллельных потоков T-SQL.
            ServicePointManager.DefaultConnectionLimit = 256;
            ServicePointManager.Expect100Continue = false;
        }

        [SqlFunction(DataAccess = DataAccessKind.None, IsDeterministic = false, IsPrecise = true)]
        public static SqlString SqlGetPositionOverHttp(SqlInt32 positionId)
        {
            if (positionId.IsNull)
            {
                return SqlString.Null;
            }

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                    "http://" + Protocol.DefaultHost + ":" + Protocol.HttpPort
                    + Protocol.HttpPathRoot + "/" + Convert.ToString(positionId.Value));
                request.Method = "GET";
                request.KeepAlive = true;
                request.Timeout = Protocol.DefaultTimeoutMs;
                request.ReadWriteTimeout = Protocol.DefaultTimeoutMs;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                {
                    if (responseStream == null)
                    {
                        return new SqlString("ERROR: пустой поток ответа");
                    }

                    string json = new StreamReader(responseStream, Encoding.UTF8).ReadToEnd();
                    return new SqlString(json);
                }
            }
            catch (Exception ex)
            {
                return new SqlString("ERROR: " + ex.Message);
            }
        }
    }
}
