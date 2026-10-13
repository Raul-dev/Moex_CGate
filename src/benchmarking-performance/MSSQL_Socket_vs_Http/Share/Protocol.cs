using System;

namespace SqlPerf
{
    /// <summary>
    /// Декларации сетевого протокола сравнения HTTP vs TCP (общие для всех проектов solution).
    /// Формат датаграммы: запрос — 4 байта (int32 positionId, little-endian);
    /// ответ — ровно <see cref="PayloadByteLength"/> байт UTF-8 JSON, дополненный пробелами до фиксированной длины.
    /// </summary>
    public static class Protocol
    {
        /// <summary>Порт TCP-сервера PositionTcpService (docker-маппинг 8888->8888).</summary>
        public const int TcpPort = 8888;

        /// <summary>Порт HTTP-сервера PositionHttpService (docker-маппинг 8080->8080).</summary>
        public const int HttpPort = 8080;

        /// <summary>Длина запроса на проводе: int32 positionId, little-endian.</summary>
        public const int RequestByteLength = 4;

        /// <summary>Фиксированная длина ответа на проводе: 5 КБ.</summary>
        public const int PayloadByteLength = 5120;

        /// <summary>Хост сервисов по умолчанию (для локального запуска тестов и SQL CLR).</summary>
        public const string DefaultHost = "127.0.0.1";

        /// <summary>Корневой HTTP-маршрут, отдающий фиксированный 5 КБ JSON (подставляется positionId).</summary>
        public const string HttpPathRoot = "/position";

        /// <summary>Таймаут сетевых обращений по умолчанию, мс (и TCP, и HTTP).</summary>
        public const int DefaultTimeoutMs = 5000;
    }
}
