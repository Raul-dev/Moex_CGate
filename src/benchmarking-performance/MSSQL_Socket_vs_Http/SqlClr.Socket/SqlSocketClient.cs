using System;
using System.Collections.Concurrent;
using System.Data.SqlTypes;
using System.Net.Sockets;
using System.Text;
using Microsoft.SqlServer.Server;
using SqlPerf;

namespace SqlClr
{
    /// <summary>
    /// SQL CLR-функция: запрос 5 КБ «котировок позиции» по постоянному TCP-сокету
    /// (4 байта запроса -> ровно Protocol.PayloadSize байт ответа).
    /// Приложение рассчитано на PERMISSION_SET = UNSAFE: прямой доступ к сокетам.
    /// </summary>
    public static class SqlSocketClient
    {
        private static readonly ConcurrentBag<TcpClient> Pool = new ConcurrentBag<TcpClient>();

        [SqlFunction(DataAccess = DataAccessKind.None, IsDeterministic = false, IsPrecise = true)]
        public static SqlString SqlGetPositionFast(SqlInt32 positionId)
        {
            if (positionId.IsNull)
            {
                return SqlString.Null;
            }

            TcpClient client;
            if (!TryRent(out client))
            {
                // Прогретого соединения в пуле нет: открываем новое постоянное соединение.
                try
                {
                    client = new TcpClient(Protocol.DefaultHost, Protocol.TcpPort) { NoDelay = true };
                    client.ReceiveTimeout = Protocol.DefaultTimeoutMs;
                    client.SendTimeout = Protocol.DefaultTimeoutMs;
                }
                catch (Exception ex)
                {
                    return new SqlString("ERROR: connect " + ex.Message);
                }
            }

            try
            {
                NetworkStream stream = client.GetStream();

                byte[] request = BitConverter.GetBytes(positionId.Value);
                stream.Write(request, 0, request.Length);

                byte[] response = ReadFully(stream, Protocol.PayloadByteLength);

                Pool.Add(client); // возврат соединения в пул

                string json = Encoding.UTF8.GetString(response);
                return new SqlString(json);
            }
            catch (Exception ex)
            {
                DisposeQuietly(client);
                return new SqlString("ERROR: " + ex.Message);
            }
        }

        private static bool TryRent(out TcpClient client)
        {
            TcpClient rented;
            if (Pool.TryTake(out rented))
            {
                // Из пула берём только здоровые соединения: битую кандидатуру закрываем и тестируем на отсутствие свободных.
                if (rented.Connected)
                {
                    client = rented;
                    return true;
                }

                DisposeQuietly(rented);
            }

            client = null;
            return false;
        }

        private static byte[] ReadFully(NetworkStream stream, int length)
        {
            byte[] buffer = new byte[length];
            int totalRead = 0;
            while (totalRead < length)
            {
                int read = stream.Read(buffer, totalRead, length - totalRead);
                if (read <= 0)
                {
                    throw new InvalidOperationException(
                        string.Format("Соединение разорвано сервером (прочитано {0} из {1} байт).", totalRead, length));
                }

                totalRead += read;
            }

            return buffer;
        }

        private static void DisposeQuietly(IDisposable disposable)
        {
            if (disposable != null)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // Соединение уже могло быть закрыто другой стороной; закрываем тихо.
                }
            }
        }
    }
}
