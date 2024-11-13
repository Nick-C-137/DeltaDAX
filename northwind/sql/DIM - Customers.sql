DROP VIEW IF EXISTS dwh.[~ DIM - Customers] 
GO

CREATE VIEW dwh.[~ DIM - Customers] AS

WITH
base as (
    SELECT
         [_BK - Customers] = CAST(CustomerID AS NVARCHAR(100))
        ,[CustomerID]
        ,[CompanyName]
        ,[ContactName]
        ,[ContactTitle]
        ,[Address]
        ,[City]
        ,[Region]
        ,[PostalCode]
        ,[Country]
        ,[Phone]
        ,[Fax]
        ,last_updated = GETDATE()
    FROM
        dbo.Customers
    WHERE
        CustomerID NOT IN ('ALFKI', 'ANATR')
)

SELECT
     *
FROM
    base

