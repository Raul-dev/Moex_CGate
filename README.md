# Moex_CGate

Оптимизация сохранения ордеров в базе, поступающих с Московской биржи по 
[CGate](https://ftp.moex.com/pub/ClientsAPI/Spectra/CGate)

## Prerequisites
- On Windows 10
- Install [Docker](https://www.docker.com/)
- Install [Docker Compose](https://docs.docker.com/compose/install/)
- Setup powershell in admin mode
```
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope AllUsers
```
- For dbmssql install Server 2019 and Visual Studio Community 2022
- Install powershell Visual Studio library for deployment script MSqlDeploymentFunc.psm1
```
Install-Module VSSetup -Scope AllUsers
```

## Getting started

To make it easy for you to get started with GitLab, here's a list of recommended next steps.

```
docker-compose -f docker-compose.rabbit.yml up
```
and start MQ.sln
