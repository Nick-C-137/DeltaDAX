DROP TABLE IF EXISTS #northwind_temp_customers;

WITH
base as (
    SELECT
         [_BK - Customers] = CustomerID
        ,[CustomerID]
        ,[CompanyName]
        ,[ContactName]
        ,[ContactTitle]
        ,[Address]
        ,[City]
        ,[Region]
        ,[PostalCode]
        ,[Country]u
        ,[Phone]
        ,[Fax]
    FROM
        dbo.Customers
    WHERE
        CustomerID NOT IN ('ALFKI', 'ANATR')
)

SELECT
     *
FROM
    base

