using System;
using System.Globalization;
using System.Text;

namespace SqlPerf
{
    /// <summary>
    /// Строит фиксированный 5 КБ (Protocol.PayloadByteLength) JSON-пакет «котировки позиций»
    /// (ASCII, UTF-8). Хвост дополняется пробелами до точного размера, что допустимо для JSON-парсеров.
    /// </summary>
    public static class PositionPayloadBuilder
    {
        /// <summary>Собирает свежий буфер ответа строго заданного размера.</summary>
        public static byte[] BuildPayload()
        {
            string json = BuildJson();
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length != Protocol.PayloadByteLength)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Ошибка расчёта размера пакета: получено {0} из {1} байт.",
                        bytes.Length,
                        Protocol.PayloadByteLength));
            }

            return bytes;
        }

        private static string BuildJson()
        {
            const string Head = "{\"status\":\"success\",\"positions\":[";
            const string Tail = "]}";

            var sb = new StringBuilder(Protocol.PayloadByteLength);
            sb.Append(Head);

            int id = 0;
            while (true)
            {
                string candidate = BuildItem(id);
                int commaLength = id > 0 ? 1 : 0;
                int projectedLength = sb.Length + commaLength + candidate.Length + Tail.Length;
                if (projectedLength > Protocol.PayloadByteLength)
                {
                    break;
                }

                if (commaLength > 0)
                {
                    sb.Append(',');
                }

                sb.Append(candidate);
                id++;
            }

            if (id == 0)
            {
                throw new InvalidOperationException("Размер пакета слишком мал хотя бы для одной записи.");
            }

            sb.Append(Tail);
            sb.Append(' ', Protocol.PayloadByteLength - sb.Length);
            return sb.ToString();
        }

        private static string BuildItem(int id)
        {
            return "{\"id\":" + Convert.ToString(id, CultureInfo.InvariantCulture)
                + ",\"ticker\":\"TICK" + Convert.ToString(id, CultureInfo.InvariantCulture)
                + "\",\"price\":" + Convert.ToString(100.5 + id, CultureInfo.InvariantCulture)
                + ",\"vol\":" + Convert.ToString(1000 * id, CultureInfo.InvariantCulture)
                + "}";
        }
    }
}
