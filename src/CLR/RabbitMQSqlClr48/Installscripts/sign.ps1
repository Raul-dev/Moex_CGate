# 1. Переходим в папку с компилятором, где лежит утилита sn.exe (путь, который мы нашли)
cd "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools"

# 2. Насильно подписываем файл microsoft.diagnostics.tracing.eventsource.dll вашим созданным ключом
# (Укажите реальный путь, где этот файл лежит у вас в исходниках NuGet, либо верните его на секунду в bin\Debug через Rebuild)
.\sn.exe -R "F:\Work\GitLab\gitlab26.neva.loc\shop\publicdwh_nodejs\src\dbprojects\TaxLotDB\services\CLR_Rabbit\RabbitMQSqlClr48\bin\Debug\Microsoft.Diagnostics.Tracing.EventSource.dll" "F:\Work\GitLab\gitlab26.neva.loc\shop\publicdwh_nodejs\src\dbprojects\TaxLotDB\services\TaxLotManager\TaxLotManager.SqlClr\SqlClrKey.snk"

# 3. Проверяем, что подпись успешно встала (консоль должна написать "...is valid")
.\sn.exe -v "F:\Work\GitLab\gitlab26.neva.loc\shop\publicdwh_nodejs\src\dbprojects\TaxLotDB\services\CLR_Rabbit\RabbitMQSqlClr48\bin\Debug\Microsoft.Diagnostics.Tracing.EventSource.dll"
