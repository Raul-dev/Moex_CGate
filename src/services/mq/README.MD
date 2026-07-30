Получение настроек рабита в конфиг файл

Overview->Export definitions
https://www.entityframeworktutorial.net/efcore/working-with-stored-procedure-in-ef-core.aspx
RabbitMQ http://localhost:15672
Сохранение записи в базу
https://www.youtube.com/watch?v=U6-k0TZL_Sw
https://habr.com/ru/articles/277177/
[Benchmark]
https://habr.com/ru/companies/fuse8/articles/803885/

Ручное подтверждение сообщения в Kafka
https://habr.com/ru/articles/808975/
настройка
https://www.youtube.com/watch?v=a4B5Oer1j2A
https://github.com/confluentinc/cp-demo
http://localhost:9021

'''
.\services\mq\MQ\bin\Release\net10.0

// DB → RabbitMQ
.\services\mq\MQ\bin\Release\net10.0\MQ.exe SendMsg -d CGate -t mssql
.\services\mq\MQ\bin\Release\net10.0\MQ.exe SendMsg -t mssql -s "localhost,1434" -d CGate -u CGateUser -w MyPassword321 -i 10 -a 500

// RabbitMQ → SQL (buffer-таблица, mq.Upload -> mq.UploadBuffer)
.\services\mq\MQ\bin\Release\net10.0\MQ.exe CopyMsg -d CGate -t mssql -q mq.Upload
.\services\mq\MQ\bin\Release\net10.0\MQ.exe CopyMsg -d CGate -t mssql -q mq.Upload -f -n 1000

// RabbitMQ → SQL (непрерывный consumer + ETL)
.\services\mq\MQ\bin\Release\net10.0\MQ.exe GetMsg -d CGate -t mssql
GetMsg -d CGate -t mssql -g whileget

// Kafka (опционально)
.\services\mq\MQ\bin\Release\net10.0\MQ.exe SendMsg -d CGate -t mssql -k $true -i 0 -a 200
.\services\mq\MQ\bin\Release\net10.0\MQ.exe GetMsg -d CGate -t mssql -k $true -g whileget
'''

'''
docker-compose -f docker-compose.rabbit.yml up
http://localhost:15672
docker-compose build
docker-compose -f docker-compose.kafka.yml up
http://localhost:9000
http://localhost:8082
Ent:
http://localhost:8090/swagger
http://localhost:8090/v1/mq/service/status
docker-compose -f docker-compose.sqldacpac.yml down -v
 SELECT * FROM [mq].[MessageQueue]  
 TRUNCATE TABLE [mq].[MessageQueue]  
TRUNCATE TABLE [CGate].[crs].[OrdersLogBuffer]
SELECT 
  [Message per second] = COUNT(*) / DATEDIFF(ss, MIN([CreatedAt]), MAX([CreatedAt])),
  [Message Count]      = COUNT(*),
  [Message Start]      = MIN([CreatedAt]), 
  [Message Finish]     = MAX([CreatedAt]),
  [Message Avg Length byte] = AVG(LEN([MessageBody])),
  [Load proc Finish]   = MAX([UpdatedAt])
FROM [CGate].[crs].[OrdersLogBuffer]
'''

C# confluent-kafka-dotnet