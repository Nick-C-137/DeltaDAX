/*
1) Create Easter Function
2) Create other Holidays functions
3) Create Date Tables
*/

CREATE SCHEMA _DeltaSYS;
GO

SET NOCOUNT ON;

--#region 1) [_DeltaSYS].[svf_DateGetEaster]
IF object_id(N'[_DeltaSYS].[svf_DateGetEaster]', 'FN') IS NOT NULL
BEGIN
    PRINT 'DROP FUNCTION [_DeltaSYS].[svf_DateGetEaster]'
    DROP FUNCTION [_DeltaSYS].[svf_DateGetEaster]
END
GO

PRINT 'CREATE function [_DeltaSYS].[svf_DateGetEaster] (@eYear int)' 
GO

CREATE function [_DeltaSYS].[svf_DateGetEaster] (@eYear int) 
returns datetime
as
begin
-- == HISTORIEN ==
-- ===============
--
-- Pave Gregor XIII indførte den gregorianske kalender i 1582. Samtidig med indførelsen af denne kalender blev datoen rettet med 10 dage (den 4. oktober blev efterfulgt af den 15. oktober).
--
-- Det er bemærkelsesværdigt, at den gregorianske kalender blev fastlagt, medens det (fejlagtige) geocentriske verdensbillede endnu var det vedtagne.
--
-- Den gregorianske kalender har, ligesom den julianske, skudår hvert fjerde år, men til gengæld er år delelige med 100 ikke skudår. 
-- Dog er disse hele hundredeår alligevel skudår, hvis de er delelige med 400. Denne sidste sjældne regel kom i anvendelse i år 2000, som var skudår.
--
-- Denne beregningsmetode giver en årslængde på i snit 365,2425 dage, meget tæt på det ønskede. Denne kalender giver kun en fejl på ca. 3 dage på 10.000 år.
--
-- I Danmark blev den gregorianske kalender indført den 1. marts 1700 efter forarbejde af Ole Rømer. Man stoppede med brug af den julianske kalender den 18. februar, (altså et spring i datoen på 11 dage).
--
-- Den gregorianske kalender blev imidlertid indført af paven kort efter Luther og reformationen, 
-- så mange protestantiske og ortodokse fyrstedømmer og lande valgte først at indføre den nye kalender meget senere for ikke at give indrømmelser over for modparten. 
-- Storbritannien indførte den nye kalender i 1752. Sverige og Finland overgik til kalenderen i 1753 ved at udelade 11 dage af året, den 17. februar 1753,som derved blev til den 1. marts.
--
--
-- Beregning af påskedag
-- =====================
--
-- De indviklede tabeller, som ledsagede den gregorianske reform, blev af C.F. Gauss omsat i følgende regel, der gælder for både den julianske og den gregorianske kalender: 
-- 
--		Hvis T betegner årstallet, og M og N er to tal, som vil blive forklaret nedenfor, og hvis 
--
--			a er resten ved divisionen T/19, 
--			b er resten ved divisionen T/4, 
--			c er resten ved divisionen T/7, 
--			d er resten ved divisionen (19a+M)/30, 
--			e er resten ved divisionen (2b+4c+6d+N)/7, 
-- 
--		så er påskedag den (22+d+e). marts eller den (d+e-9). april, dog med følgende undtagelser: 
-- 
--			1. Hvis d = 29 og e = 6, er påskedag ikke den 26., men den 19. april. 
--			2. Hvis d = 28 og e = 6 og desuden a > 10, er påskedag ikke den 25., men den 18. april. 
-- 
-- Tallene M og N er i den julianske kalender konstante, nemlig M = 15 og N = 6. I den gregorianske kalender skifter de ofte med århundredet og beregnes således: 
-- 
--			k er kvotienten ved divisionen T/100,
--			p er kvotienten ved divisionen (13+8k)/25, 
--			q er kvotienten ved divisionen k/4, 
--			M resten ved divisionen (15-p+k-q)/30, 
--			N er resten ved divisionen (4+k-q)/7. 
--

-- SQL Server 2005 kan ikke regne før år 1753
if @eYear<1753 set @eYear=null


declare @T int,
		@M int,
		@N int,

		@a int, @b int,@c int, @d int, @e int,

		@k int, @p int, @q int,

		@eDay int,
		@eMonth int,
		@gregorskift int

-- Set årstal
		set @T=@eYear
		set @gregorskift=1700

-- Beregning af M og N
		set @k=round(@T/100,0)
		set @p=round((13+(8*@k))/25,0)
		set @q=round(@k/4,0)

		set @M=case when @T>@gregorskift then (15-@p+@k-@q) % 30 else 15 end
		set @N=case when @T>@gregorskift then (4+@k-@q) % 7 else 6 end

-- Beregn øvrige variable
		set @a=@T % 19
		set @b=@T % 4
		set @c=@T % 7
		set @d=((19*@a)+@M) % 30
		set @e=((2*@b)+(4*@c)+(6*@d)+@N) % 7

		select @eDay=22+@d+@e
		select @eMonth=4
		
		if @eDay<=31 
			set @eMonth=3 
		else
			if @d=28 and @e=6 and @a>10
				set @eDay=18
			else
				if @d=29 and @e=6
					set @eday=19
				else
					set @eday=@d+@e-9

  return convert(datetime,cast(@T as varchar)+'/'+cast(@eMonth as varchar)+'/'+cast(@eDay as varchar),111)
end
GO
--#endregion

--#region 2) [_DeltaSYS].[tvf_DateGetHolidays]
IF object_id(N'[_DeltaSYS].[tvf_DateGetHolidays]') IS NOT NULL
BEGIN
    PRINT 'DROP FUNCTION [_DeltaSYS].[tvf_DateGetHolidays]'
    DROP FUNCTION [_DeltaSYS].[tvf_DateGetHolidays]
END
GO

PRINT 'CREATE function [_DeltaSYS].[tvf_DateGetHolidays](@Date date,@country)' 
GO


CREATE FUNCTION [_DeltaSYS].[tvf_DateGetHolidays]
(
	@Date date,
	@country nvarchar(10) = 'DK'
)
RETURNS 
@Holiday TABLE 
(
	IsHoliday tinyint,
	HolidayName nvarchar(100)
)
AS
BEGIN
	DECLARE @VariableHoliday nvarchar(100)
		   ,@FixedHoliday nvarchar(100)

	-- For Denmark (DK)
	IF @country = 'DK'
	BEGIN
		-- Add variable holidays, calculated from easter day
		SET @VariableHoliday =
		CASE DATEDIFF(DAY,[_DeltaSYS].[svf_DateGetEaster](DATEPART(YEAR,@Date)),@Date)
			WHEN -7 THEN 'Palmesøndag'
			WHEN -3 THEN 'Skærtorsdag'
			WHEN -2 THEN 'Langfredag'
			WHEN 0  THEN 'Påskedag'
			WHEN 1  THEN '2. Påskedag'
			--WHEN 26 THEN 'St. Bededag'
			WHEN 39 THEN 'Kristi Himmelfart'
			WHEN 49 THEN 'Pinse'
			WHEN 50 THEN '2. Pinsedag'
		END

		-- Add fixed holidays
		SET @FixedHoliday =
		CASE 
			WHEN DATEPART(MONTH,@Date) = 1  AND DATEPART(DAY,@Date) = 1  THEN 'Nytårsdag'
			WHEN DATEPART(MONTH,@Date) = 5  AND DATEPART(DAY,@Date) = 1  THEN '1. Maj'
			WHEN DATEPART(MONTH,@Date) = 6  AND DATEPART(DAY,@Date) = 5  THEN 'Grundlovsdag'
			WHEN DATEPART(MONTH,@Date) = 12 AND DATEPART(DAY,@Date) = 24 THEN 'Juleaften'
			WHEN DATEPART(MONTH,@Date) = 12 AND DATEPART(DAY,@Date) = 25 THEN '1. juledag'
			WHEN DATEPART(MONTH,@Date) = 12 AND DATEPART(DAY,@Date) = 26 THEN '2. juledag'
			WHEN DATEPART(MONTH,@Date) = 12 AND DATEPART(DAY,@Date) = 31 THEN 'Nytårsaften'
		END
	END

	-- Set return result
	IF @VariableHoliday IS NOT NULL AND @FixedHoliday IS NOT NULL 
	BEGIN -- If both variable and fixed are present, both are in text returned
		INSERT INTO @Holiday(IsHoliday,HolidayName)
		SELECT 1, @VariableHoliday + ', ' + @FixedHoliday
	END
	ELSE IF @VariableHoliday IS NULL AND @FixedHoliday IS NULL
	BEGIN -- If none is present return no holiday
		INSERT INTO @Holiday(IsHoliday,HolidayName)
		SELECT 0, '-'
	END
	ELSE
	BEGIN -- insert holiday name for the one which is set
		INSERT INTO @Holiday(IsHoliday,HolidayName)
		SELECT 1, COALESCE(@VariableHoliday,@FixedHoliday)
	END
	
	RETURN 
END
GO
--#endregion

--#region 3) DateTable
/*
1) Data Factory
2) Generer Dato Tabel (Schema: )
*/

SET DATEFIRST 1;/*Define monday as the first day of the week - Denmark standard*/

DECLARE @StartDate AS DATE   = '1900-01-01';
DECLARE @EndDate AS DATE     = '2099-12-31';

-- DECLARE @StartDate AS DATE      = CAST('@{pipeline().parameters.StartDate}' as Date);
-- DECLARE @EndDate AS DATE        = CAST('@{pipeline().parameters.EndDate}' as Date);

--#region Generate Dates
DROP TABLE IF EXISTS #Dates;
	WITH DateSeries AS 
    (
		SELECT [Date] = @StartDate
		UNION ALL
		SELECT DATEADD(DAY, 1, [Date])
		FROM DateSeries
		WHERE [Date] < @EndDate
    )
	SELECT [Date]
	INTO #Dates
	FROM DateSeries
	OPTION (MAXRECURSION 0);
--#endregion



DROP TABLE IF EXISTS [_DeltaSYS].[Dates];
SELECT 
    --#region Date
     [Date]             = CAST([Date] AS Date)
	,[Date int]         = CAST(FORMAT([Date], 'yyyyMMdd') AS int)
	--#endregion

    --#region Year
    ,[Year]             = YEAR([Date])
    ,[Days in Year] = DATEDIFF(DAY, CAST(YEAR([Date]) as nvarchar(4)) + '-01-01',CAST(YEAR([Date]) as nvarchar(4)) + '-12-31')
    --#endregion

    --#region Month
	,[Year Month]       = CAST(YEAR([Date]) AS VARCHAR(4)) + '-' + FORMAT(DATEPART(MONTH, [Date]), '00')
	,[Year Month int]   = CAST(YEAR([Date]) AS VARCHAR(4)) + FORMAT(DATEPART(MONTH, [Date]), '00')
	
    -- Month Year In English and Danish
	,[Month Year (ENG)] = 
        CASE MONTH([Date])
            WHEN 1  THEN 'January' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 2  THEN 'February' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 3  THEN 'March' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 4  THEN 'April' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 5  THEN 'May' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 6  THEN 'June' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 7  THEN 'July' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 8  THEN 'August' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 9  THEN 'September' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 10 THEN 'October' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 11 THEN 'November' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 12 THEN 'December' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            ELSE null
		END
	,[Month Year (DK)] = 
        CASE MONTH([Date])
            WHEN 1  THEN 'Januar' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 2  THEN 'Februar' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 3  THEN 'Marts' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 4  THEN 'April' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 5  THEN 'Maj' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 6  THEN 'Juni' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 7  THEN 'Juli' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 8  THEN 'August' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 9  THEN 'September' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 10 THEN 'Oktober' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 11 THEN 'November' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 12 THEN 'December' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            ELSE null
        END
    ,[Month Year (ENG) short] = 
        CASE MONTH([Date])
            WHEN 1  THEN 'Jan' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 2  THEN 'Feb' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 3  THEN 'Mar' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 4  THEN 'Apr' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 5  THEN 'May' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 6  THEN 'Jun' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 7  THEN 'Jul' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 8  THEN 'Aug' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 9  THEN 'Sep' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 10 THEN 'Oct' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 11 THEN 'Nov' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 12 THEN 'Dec' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            ELSE null
		END
	,[Month Year (DK) short] = 
        CASE MONTH([Date])
            WHEN 1  THEN 'Jan' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 2  THEN 'Feb' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 3  THEN 'Mar' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 4  THEN 'Apr' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 5  THEN 'Maj' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 6  THEN 'Jun' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 7  THEN 'Jul' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 8  THEN 'Aug' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 9  THEN 'Sep' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 10 THEN 'Okt' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 11 THEN 'Nov' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            WHEN 12 THEN 'Dec' + ' ' + CAST(year([Date]) AS VARCHAR(4))
            ELSE null
        END
    
    ,[Month] = Month([Date])
    ,[Month Name (ENG)] = 
        CASE MONTH([Date])
            WHEN 1  THEN 'January'
            WHEN 2  THEN 'February'
            WHEN 3  THEN 'March'
            WHEN 4  THEN 'April'
            WHEN 5  THEN 'May'
            WHEN 6  THEN 'June'
            WHEN 7  THEN 'July'
            WHEN 8  THEN 'August'
            WHEN 9  THEN 'September'
            WHEN 10 THEN 'October'
            WHEN 11 THEN 'November'
            WHEN 12 THEN 'December'
            ELSE null
		END
	,[Month Name (DK)] = 
        CASE MONTH([Date])
            WHEN 1  THEN 'Januar'
            WHEN 2  THEN 'Februar'
            WHEN 3  THEN 'Marts'
            WHEN 4  THEN 'April'
            WHEN 5  THEN 'Maj'
            WHEN 6  THEN 'Juni'
            WHEN 7  THEN 'Juli'
            WHEN 8  THEN 'August'
            WHEN 9  THEN 'September'
            WHEN 10 THEN 'Oktober'
            WHEN 11 THEN 'November'
            WHEN 12 THEN 'December'
            ELSE null
        END

	,[Day of Month]         = DAY([Date])
	,[Days in Month]        = DATEDIFF(d, dateadd(m, datediff(m, 0, [Date]), 0), EOMONTH([Date])) + 1
	,[First Date of Month]  = CAST(dateadd(m, datediff(m, 0, [Date]), 0) AS [Date])
	,[End Date of Month]    = EOMONTH([Date])


	--#endregion Month
	
	--#region Quarter
	,[Quarter int] = 
        CASE 
            WHEN MONTH([Date]) <= 3 THEN 1
            WHEN MONTH([Date]) <= 6 THEN 2
            WHEN MONTH([Date]) <= 9 THEN 3
            ELSE 4
	    END 
	,[Quarter] = 
        CASE 
            WHEN MONTH([Date]) <= 3 THEN 'Q1'
            WHEN MONTH([Date]) <= 6 THEN 'Q2'
            WHEN MONTH([Date]) <= 9 THEN 'Q3'
            ELSE 'Q4'
	    END 
    ,[Year Quarter] =
        CASE 
            WHEN MONTH([Date]) <= 3 THEN CAST(year([Date]) AS VARCHAR(4)) + '-Q1'
            WHEN MONTH([Date]) <= 6 THEN CAST(year([Date]) AS VARCHAR(4)) + '-Q2'
            WHEN MONTH([Date]) <= 9 THEN CAST(year([Date]) AS VARCHAR(4)) + '-Q3'
            ELSE CAST(year([Date]) AS VARCHAR(4)) + '-Q4'
	    END 
	,[Year Quarter int] = 
	    CAST(CASE 
            WHEN MONTH([Date]) <= 3 THEN CAST(year([Date]) AS VARCHAR(4)) + '1'
            WHEN MONTH([Date]) <= 6 THEN CAST(year([Date]) AS VARCHAR(4)) + '2'
            WHEN MONTH([Date]) <= 9 THEN CAST(year([Date]) AS VARCHAR(4)) + '3'
            ELSE CAST(year([Date]) AS VARCHAR(4)) + '4'
		END AS int)
	,[Days in Quarter] = 
        CASE 
            WHEN MONTH([Date]) <= 3 THEN DATEDIFF(DAY, CAST(YEAR([Date]) as nvarchar(4)) + '-01-01',EOMONTH(CAST(YEAR([Date]) as nvarchar(4)) + '-03-01'))
            WHEN MONTH([Date]) <= 6 THEN DATEDIFF(DAY, CAST(YEAR([Date]) as nvarchar(4)) + '-04-01',EOMONTH(CAST(YEAR([Date]) as nvarchar(4)) + '-06-01'))
            WHEN MONTH([Date]) <= 9 THEN DATEDIFF(DAY, CAST(YEAR([Date]) as nvarchar(4)) + '-07-01',EOMONTH(CAST(YEAR([Date]) as nvarchar(4)) + '-09-01'))
            ELSE DATEDIFF(DAY, CAST(YEAR([Date]) as nvarchar(4)) + '-10-01',EOMONTH(CAST(YEAR([Date]) as nvarchar(4)) + '-12-01'))
	    END
	--#endregion Quarter

    --#region Week Attributes
	,[Week No]      = CAST(DATEPART(Iso_week, [Date]) AS int)
	,[Next Week No] = DATEPART(Iso_week, DATEADD(wk, 1, [Date]))
	
    ,[Year Week (Calendar)] = 
        CASE /*We handle year start / year end complexity*/
            WHEN Month([Date]) = 12 AND DAY([Date]) > 17 AND DATEPART(ISO_WEEK, [Date]) = 1 THEN CAST((YEAR([Date]) + 1) AS VARCHAR(4))
            WHEN Month([Date]) = 1 AND DAY([Date]) < 17 AND DATEPART(ISO_WEEK, [Date]) > 51 THEN CAST((YEAR([Date]) - 1) AS VARCHAR(4))
            ELSE CAST((YEAR([Date])) AS VARCHAR(4))
		END + '-W' + FORMAT(DATEPART(ISO_WEEK, [Date]), '00')
	,[Year Week (Calendar) int] =
        CAST(CASE 
            WHEN Month([Date]) = 12 AND DAY([Date]) > 17 AND DATEPART(ISO_WEEK, [Date]) = 1 THEN CAST((YEAR([Date]) + 1) AS VARCHAR(4))
            WHEN Month([Date]) = 1 AND DAY([Date]) < 17 AND DATEPART(ISO_WEEK, [Date]) > 51 THEN CAST((YEAR([Date]) - 1) AS VARCHAR(4))
            ELSE CAST((YEAR([Date])) AS VARCHAR(4))
		END + FORMAT(DATEPART(ISO_WEEK, [Date]), '00') AS int)

	,[Week Day No]  = DATEPART(Weekday, [Date])

	,[Week Day (ENG)] = 
        CASE DATEPART(Weekday, [Date])
            WHEN 1 THEN 'Monday'
            WHEN 2 THEN 'Tuesday'
            WHEN 3 THEN 'Wednesday'
            WHEN 4 THEN 'Thursday'
            WHEN 5 THEN 'Friday'
            WHEN 6 THEN 'Saturday'
            WHEN 7 THEN 'Sunday'
            ELSE null
		END
	,[Week Day (DK)] = 
        CASE DATEPART(Weekday, [Date])
            WHEN 1 THEN 'Mandag'
            WHEN 2 THEN 'Tirsdag'
            WHEN 3 THEN 'Onsdag'
            WHEN 4 THEN 'Torsdag'
            WHEN 5 THEN 'Fredag'
            WHEN 6 THEN 'Lørdag'
            WHEN 7 THEN 'Søndag'
            ELSE null
		END
	
    ,[First Date of Week] = dateadd(ww, datediff(ww, 0, dateadd(day, - 1, [Date])), 0)
    --#endregion

    --#region Holidays
        /*
        ### In this region we can develop holiday calculations ###
        */
    ,[IsHoliday (DK)]           = [oa_holiday].[IsHoliday]
    ,[HolidayName (DK)]        = [oa_holiday].[HolidayName]

    --#endregion


into [_DeltaSYS].[Dates]
FROM #Dates
OUTER APPLY [_DeltaSYS].[tvf_DateGetHolidays]([Date], 'DK') oa_holiday
;

--#endregion