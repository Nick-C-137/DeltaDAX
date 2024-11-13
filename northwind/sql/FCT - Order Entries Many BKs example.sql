DROP VIEW IF EXISTS dwh.[$ Order Entries Many BKs Example] 
GO

CREATE VIEW dwh.[$ Order Entries Many BKs Example] AS
WITH 

base as (
    SELECT
        *
    FROM
        dbo.Orders
)

SELECT
    [_BK - Customers]                       = CAST(a.CustomerID AS NVARCHAR(100))
   ,[_BK - Employees]                       = CAST(a.EmployeeID AS NVARCHAR(100))
   ,[_BK - Products]                        = CAST(b.ProductID AS NVARCHAR(100))
   ,[_BK - Shippers]                        = CAST(a.ShipVia AS NVARCHAR(100))
   ,[_BK - Locations | Ship To *]             = CONCAT(
                                                 a.ShipAddress
                                                ,a.ShipCity
                                                ,a.ShipRegion
                                                ,a.ShipPostalCode
                                                ,a.ShipCountry
                                            )
    ,[_BK - Locations | Bill To]            = CONCAT(
                                                 a.ShipAddress
                                                ,a.ShipCity
                                                ,a.ShipRegion
                                                ,a.ShipPostalCode
                                                ,a.ShipCountry
                                            )
   ,[_DK - Calendar | Order Date *]         = CAST(a.OrderDate as DATE)
   ,[_DK - Calendar | Required Date]        = CAST(a.RequiredDate as DATE)
   ,[_DK - Calendar | Shipped Date]         = CAST(a.ShippedDate as DATE)
   ,[_MS - Freight]                         = a.Freight
   ,[_MS - Quantity]                        = b.Quantity
   ,[_MS - Discount]                        = b.Discount
   ,[_MS - Unit Price]                      = b.UnitPrice
   ,last_updated = GETDATE()
FROM
    base as a
JOIN
    dbo.[Order Details] as b
    ON a.OrderID = b.OrderID
LEFT JOIN
    dbo.Customers as c
    ON a.CustomerID = c.CustomerID