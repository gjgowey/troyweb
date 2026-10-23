CREATE TABLE [ApplicationStatuses] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(300) NOT NULL,
    [Name] nvarchar(300) NOT NULL,
    [SortOrder] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ApplicationStatuses] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [UnitTypes] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(300) NOT NULL,
    [Name] nvarchar(300) NOT NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_UnitTypes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Applicants] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [MiddleInitial] nvarchar(1) NULL,
    [Title] nvarchar(20) NULL,
    [Email] nvarchar(300) NOT NULL,
    [Phone] nvarchar(300) NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [SeedKey] nvarchar(100) NULL,
    CONSTRAINT [PK_Applicants] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Applicants_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [PropertyManagers] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [MiddleInitial] nvarchar(1) NULL,
    [Title] nvarchar(20) NULL,
    [Email] nvarchar(300) NOT NULL,
    [Phone] nvarchar(300) NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [SeedKey] nvarchar(100) NULL,
    CONSTRAINT [PK_PropertyManagers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PropertyManagers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Properties] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(300) NOT NULL,
    [Address] nvarchar(300) NOT NULL,
    [PropertyManagerId] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [SeedKey] nvarchar(100) NULL,
    CONSTRAINT [PK_Properties] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Properties_PropertyManagers_PropertyManagerId] FOREIGN KEY ([PropertyManagerId]) REFERENCES [PropertyManagers] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Units] (
    [Id] int NOT NULL IDENTITY,
    [PropertyId] int NOT NULL,
    [Number] nvarchar(300) NOT NULL,
    [Bedrooms] int NOT NULL,
    [UnitTypeId] int NOT NULL,
    [MonthlyRent] decimal(10,2) NOT NULL,
    [LockVersion] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [SeedKey] nvarchar(100) NULL,
    CONSTRAINT [PK_Units] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Unit_Bedrooms] CHECK (Bedrooms >= 0),
    CONSTRAINT [CK_Unit_Rent] CHECK (MonthlyRent > 0),
    CONSTRAINT [FK_Units_Properties_PropertyId] FOREIGN KEY ([PropertyId]) REFERENCES [Properties] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Units_UnitTypes_UnitTypeId] FOREIGN KEY ([UnitTypeId]) REFERENCES [UnitTypes] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Applications] (
    [Id] int NOT NULL IDENTITY,
    [ApplicantId] int NOT NULL,
    [UnitId] int NOT NULL,
    [StatusId] int NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [SubmittedAtUtc] datetime2 NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [MiddleInitial] nvarchar(1) NULL,
    [Title] nvarchar(20) NULL,
    [Phone] nvarchar(300) NOT NULL,
    [Email] nvarchar(300) NOT NULL,
    [CurrentAddress] nvarchar(300) NOT NULL,
    [InformationSaved] bit NOT NULL,
    [HistorySaved] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [SeedKey] nvarchar(100) NULL,
    CONSTRAINT [PK_Applications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Applications_Applicants_ApplicantId] FOREIGN KEY ([ApplicantId]) REFERENCES [Applicants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Applications_ApplicationStatuses_StatusId] FOREIGN KEY ([StatusId]) REFERENCES [ApplicationStatuses] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Applications_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Leases] (
    [Id] int NOT NULL IDENTITY,
    [UnitId] int NOT NULL,
    [RentalApplicationId] int NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Leases] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Lease_Term] CHECK (EndDate > StartDate),
    CONSTRAINT [FK_Leases_Applications_RentalApplicationId] FOREIGN KEY ([RentalApplicationId]) REFERENCES [Applications] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leases_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Residences] (
    [Id] int NOT NULL IDENTITY,
    [RentalApplicationId] int NOT NULL,
    [Address] nvarchar(300) NOT NULL,
    [LandlordName] nvarchar(300) NOT NULL,
    [LandlordPhone] nvarchar(300) NOT NULL,
    [MoveIn] date NOT NULL,
    [MoveOut] date NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Residences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Residences_Applications_RentalApplicationId] FOREIGN KEY ([RentalApplicationId]) REFERENCES [Applications] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [StatusChanges] (
    [Id] int NOT NULL IDENTITY,
    [RentalApplicationId] int NOT NULL,
    [FromStatus] nvarchar(300) NOT NULL,
    [ToStatus] nvarchar(300) NOT NULL,
    [ActorUserId] nvarchar(450) NOT NULL,
    [ActorName] nvarchar(300) NOT NULL,
    [ChangedAtUtc] datetime2 NOT NULL,
    [Comment] nvarchar(2000) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_StatusChanges] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StatusChanges_Applications_RentalApplicationId] FOREIGN KEY ([RentalApplicationId]) REFERENCES [Applications] ([Id]) ON DELETE CASCADE
);
GO


CREATE UNIQUE INDEX [IX_Applicants_SeedKey] ON [Applicants] ([SeedKey]) WHERE [SeedKey] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_Applicants_UserId] ON [Applicants] ([UserId]);
GO


CREATE INDEX [IX_Applications_ApplicantId_CreatedAtUtc] ON [Applications] ([ApplicantId], [CreatedAtUtc] DESC);
GO


CREATE INDEX [IX_Applications_CreatedAtUtc] ON [Applications] ([CreatedAtUtc] DESC);
GO


CREATE UNIQUE INDEX [IX_Applications_SeedKey] ON [Applications] ([SeedKey]) WHERE [SeedKey] IS NOT NULL;
GO


CREATE INDEX [IX_Applications_StatusId_CreatedAtUtc] ON [Applications] ([StatusId], [CreatedAtUtc] DESC);
GO


CREATE INDEX [IX_Applications_UnitId_StatusId_CreatedAtUtc] ON [Applications] ([UnitId], [StatusId], [CreatedAtUtc] DESC);
GO


CREATE UNIQUE INDEX [IX_ApplicationStatuses_Code] ON [ApplicationStatuses] ([Code]);
GO


CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO


CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO


CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO


CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO


CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_Leases_RentalApplicationId] ON [Leases] ([RentalApplicationId]);
GO


CREATE INDEX [IX_Leases_UnitId_StartDate_EndDate] ON [Leases] ([UnitId], [StartDate], [EndDate]);
GO


CREATE INDEX [IX_Properties_Name] ON [Properties] ([Name]);
GO


CREATE INDEX [IX_Properties_PropertyManagerId] ON [Properties] ([PropertyManagerId]);
GO


CREATE UNIQUE INDEX [IX_Properties_SeedKey] ON [Properties] ([SeedKey]) WHERE [SeedKey] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_PropertyManagers_SeedKey] ON [PropertyManagers] ([SeedKey]) WHERE [SeedKey] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_PropertyManagers_UserId] ON [PropertyManagers] ([UserId]);
GO


CREATE INDEX [IX_Residences_RentalApplicationId] ON [Residences] ([RentalApplicationId]);
GO


CREATE INDEX [IX_StatusChanges_RentalApplicationId] ON [StatusChanges] ([RentalApplicationId]);
GO


CREATE UNIQUE INDEX [IX_Units_PropertyId_Number] ON [Units] ([PropertyId], [Number]);
GO


CREATE UNIQUE INDEX [IX_Units_SeedKey] ON [Units] ([SeedKey]) WHERE [SeedKey] IS NOT NULL;
GO


CREATE INDEX [IX_Units_UnitTypeId] ON [Units] ([UnitTypeId]);
GO


CREATE UNIQUE INDEX [IX_UnitTypes_Code] ON [UnitTypes] ([Code]);
GO


