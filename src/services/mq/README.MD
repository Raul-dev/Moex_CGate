Получение настроек рабита в конфиг файл
Overview->Export definitions
https://www.entityframeworktutorial.net/efcore/working-with-stored-procedure-in-ef-core.aspx
RabbitMQ http://localhost:15672
Сохранение записи в базу
https://www.youtube.com/watch?v=U6-k0TZL_Sw
.\src\services\mq\MQ\bin\Debug\net8.0
//отправляем сообщения из SQL in Rabbit
/For Rabbit
./MQ.exe SendMsg -d CGate -t mssql -i 0 -a 200

/For kafka
./MQ.exe SendMsg -d CGate -t mssql -k $true -i 0 -a 200
//For Rabbit в SQL
./MQ.exe GetMsg -d CGate -t mssql 
for VS debug command: GetMsg -d CGate -t mssql -g whileget 
/For kafka
./MQ.exe GetMsg -d CGate -t mssql -k $true

docker-compose -f docker-compose.rabbit.yml up
http://localhost:15672