DROP VIEW IF EXISTS dwh.[~ DIM - Products] 
GO

CREATE VIEW dwh.[~ DIM - Products] AS
WITH

base as (
    SELECT
         [_BK - Products] = ProductID
        ,[ProductID]
        ,[ProductName]
        ,[SupplierID]
        ,[CategoryID]
        ,[QuantityPerUnit]
        ,[UnitPrice]
        ,[UnitsInStock]
        ,[UnitsOnOrder]
        ,[ReorderLevel]
        ,[Discontinued]
    FROM
        dbo.Products
)

SELECT
     *
FROM
    base


