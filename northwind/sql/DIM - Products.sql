DROP VIEW IF EXISTS dwh.[~ DIM - Products] 
GO

CREATE VIEW dwh.[~ DIM - Products] AS
WITH

base as (
    SELECT
         [_BK - Products] = CAST(ProductID AS NVARCHAR(100))
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
        ,last_updated = GETDATE()
    FROM
        dbo.Products
)

SELECT
     *
FROM
    base


