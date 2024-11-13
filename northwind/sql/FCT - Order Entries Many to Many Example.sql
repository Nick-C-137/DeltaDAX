DROP VIEW IF EXISTS dwh.[$ Order Entries Many to Many Example]
GO 

CREATE VIEW dwh.[$ Order Entries Many to Many Example] AS
WITH 

base as (
    SELECT
        *
    FROM
        dbo.Orders
)

SELECT
    [_BK - Customers]           = a.CustomerID
   ,[_BK - Employees]           = a.EmployeeID
   ,[_BK - Shippers]            = a.ShipVia
   ,[_BK - Locations]           = CONCAT(
                                     a.ShipAddress
                                    ,a.ShipCity
                                    ,a.ShipRegion
                                    ,a.ShipPostalCode
                                    ,a.ShipCountry
                                )
   ,[_DK - Calendar | Order Date *]         = CAST(a.OrderDate as DATE)
   ,[_DK - Calendar | Required Date]        = CAST(a.RequiredDate as DATE)
   ,[_DK - Calendar | Shipped Date]         = CAST(a.ShippedDate as DATE)
   ,[_MM - Products / CategoryID <>]        = d.CategoryID
   ,[_MS - Freight]                         = a.Freight
   ,[_MS - Quantity]                        = b.Quantity
   ,[_MS - Discount]                        = b.Discount
   ,[_MS - Unit Price]                      = b.UnitPrice
FROM
    base as a
JOIN
    dbo.[Order Details] as b
    ON a.OrderID = b.OrderID
LEFT JOIN
    dbo.Customers as c
    ON a.CustomerID = c.CustomerID
LEFT JOIN
    dbo.Products as d
    ON b.ProductID = d.ProductID