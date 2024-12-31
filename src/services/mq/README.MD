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
.\services\mq\MQ\bin\Debug\net9.0
//отправляем сообщения из SQL in Rabbit

/For Rabbit
.\services\mq\MQ\bin\Debug\net9.0\MQ.exe SendMsg -d CGateBuf -t mssql -i 2
.\services\mq\MQ\bin\Debug\net9.0\MQ.exe SendMsg -d CGate -t mssql -i 0 -a 200

/For kafka
./MQ.exe SendMsg -d CGate -t mssql -k $true -i 0 -a 200
.\services\mq\MQ\bin\Debug\net9.0\MQ.exe SendMsg -d CGate -t mssql -k $true -i 0 -a 200


//For Rabbit в SQL
.\services\mq\MQ\bin\Debug\net9.0\MQ.exe GetMsg -d CGate -t mssql 

for VS debug command (сообщения забираются в цикле): GetMsg -d CGate -t mssql -g whileget 

/For kafka
.\services\mq\MQ\bin\Debug\net9.0\MQ.exe GetMsg -d CGate -t mssql -k $true -g whileget 
'''

'''
docker-compose -f docker-compose.rabbit.yml up
http://localhost:15672
docker-compose build
docker-compose -f docker-compose.kafka.yml up
http://localhost:9000
http://localhost:8082
Ent:
http://localhost:9021
 SELECT * FROM [dbo].[msgqueue]  
 TRUNCATE TABLE [dbo].[msgqueue]  
TRUNCATE TABLE [CGate].[crs].[orders_log_buffer]
SELECT 
  [Message per second] = COUNT(*) / DATEDIFF(ss,MIN(dt_create), MAX(dt_create)),
  [Message Count]      = COUNT(*),
  [Message Start]      = MIN(dt_create), 
  [Message Finish]     = MAX(dt_create),
  [Message Avg Length byte] = AVG(LEN(msg)),
  [Load proc Finish]   = MAX([dt_update])
FROM [CGate].[crs].[orders_log_buffer]  
'''

C# confluent-kafka-dotnet