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
   ,[_BK - Products]            = b.ProductID
   ,[_BK - Shippers]            = a.ShipVia
   ,[_BK - Locations]           = CONCAT(
                                     a.ShipAddress
                                    ,a.ShipCity
                                    ,a.ShipRegion
                                    ,a.ShipPostalCode
                                    ,a.ShipCountry
                                )
    
   ,[_DK - Calendar / Order Date *]         = CAST(a.OrderDate as DATE)
   ,[_DK - Calendar / Required Date]        = CAST(a.RequiredDate as DATE)
   ,[_DK - Calendar / Shipped Date]         = CAST(a.ShippedDate as DATE)
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