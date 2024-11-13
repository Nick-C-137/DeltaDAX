SELECT
     [_BK - Employees] = EmployeeID
    ,[EmployeeID]
    ,[LastName]
    ,[FirstName]
    ,[Title]                /*DeltaSQL_Desc - Title: Title is bla bla*/
    ,[TitleOfCourtesy]      /*DeltaSQL_Desc - TitleOfCourtesy: TitleOfCourtesy is bla bla*/
    ,[BirthDate]            /*DeltaSQL_DescXX - BirthDateERROR: BirthDate definition have an error*/
    ,[HireDate]
    ,[Address]
    ,[City]
    ,[Region]
    ,[PostalCode]
    ,[Country]
    ,[HomePhone]
    ,[Extension]
    ,[Notes]
    ,[ReportsTo]
    ,[PhotoPath]
FROM
    dbo.Employees
WHERE
    EmployeeID NOT IN (1, 2)