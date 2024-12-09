# Moex_CGate

Оптимизация сохранения ордеров в базе, поступающих с Московской биржи по CGate

![Многопоточная архитектура сохранения ордеров moex в базе](./doc/schema.png)

 Библиотека P2 CGate представляет собой набор следующих компонент:
 • системные библиотеки Plaza-2
 • маршрутизатор сообщений P2MQRouter
 • шлюзовая библиотека cgate
 • заголовочный файл с описанием API - cgate.h
 Все эти компоненты необходимы для разработки с использованием библиотеки P2 CGate
 и находятся в свободном доступе на [ftp.moex.com](https://ftp.moex.com/pub/ClientsAPI/Spectra/CGate)

- [Протоколы передачи финансовых данных. Инструкция по применению](https://habr.com/ru/companies/moex/articles/261369/)

## Prerequisites

- On Windows 10
- Install [Docker](https://www.docker.com/)
- Install [Docker Compose](https://docs.docker.com/compose/install/)
- Setup powershell in admin mode

```
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope AllUsers
```

- Install  MS SQL Server 2022 and Visual Studio Community 2022
- Install powershell Visual Studio library for deployment script MSqlDeploymentFunc.psm1

```
Install-Module VSSetup -Scope AllUsers
```

## Getting started

 Here's a list of recommended next steps.

```
docker-compose -f docker-compose.rabbit.yml up
```

and start MQ.sln
