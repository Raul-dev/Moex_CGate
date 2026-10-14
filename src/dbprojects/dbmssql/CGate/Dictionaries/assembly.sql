--CREATE DATABASE CGate45
--USE [CGate45]
--CREATE ROLE rmq
--CREATE schema rmq
EXEC dbo.sp_configure 'show advanced options', 1;
RECONFIGURE;
EXEC dbo.sp_configure 'clr enabled', 1;
RECONFIGURE;

