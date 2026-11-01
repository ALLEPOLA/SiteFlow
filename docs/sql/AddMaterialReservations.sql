START TRANSACTION;

ALTER TABLE `MaterialIssueLines` ADD `ReservationId` int NULL;

CREATE TABLE `MaterialReservations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ClientRequestId` varchar(36) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectId` int NOT NULL,
    `MaterialRequestId` int NOT NULL,
    `MaterialId` int NOT NULL,
    `Quantity` decimal(18,3) NOT NULL,
    `ConsumedQuantity` decimal(18,3) NOT NULL,
    `ReleasedQuantity` decimal(18,3) NOT NULL,
    `Reason` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `ReleaseReason` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedByUserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `ReleasedAtUtc` datetime(6) NULL,
    CONSTRAINT `PK_MaterialReservations` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_MaterialReservations_Quantities` CHECK (`Quantity` > 0 AND `ConsumedQuantity` >= 0 AND `ReleasedQuantity` >= 0 AND `ConsumedQuantity` + `ReleasedQuantity` <= `Quantity`),
    CONSTRAINT `FK_MaterialReservations_AspNetUsers_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_MaterialReservations_MaterialRequests_MaterialRequestId` FOREIGN KEY (`MaterialRequestId`) REFERENCES `MaterialRequests` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_MaterialReservations_Materials_MaterialId` FOREIGN KEY (`MaterialId`) REFERENCES `Materials` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_MaterialReservations_Projects_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Projects` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_MaterialIssueLines_ReservationId` ON `MaterialIssueLines` (`ReservationId`);

CREATE UNIQUE INDEX `IX_MaterialReservations_ClientRequestId` ON `MaterialReservations` (`ClientRequestId`);

CREATE INDEX `IX_MaterialReservations_CreatedByUserId` ON `MaterialReservations` (`CreatedByUserId`);

CREATE INDEX `IX_MaterialReservations_MaterialId` ON `MaterialReservations` (`MaterialId`);

CREATE INDEX `IX_MaterialReservations_MaterialRequestId` ON `MaterialReservations` (`MaterialRequestId`);

CREATE INDEX `IX_MaterialReservations_ProjectId_MaterialId` ON `MaterialReservations` (`ProjectId`, `MaterialId`);

ALTER TABLE `MaterialIssueLines` ADD CONSTRAINT `FK_MaterialIssueLines_MaterialReservations_ReservationId` FOREIGN KEY (`ReservationId`) REFERENCES `MaterialReservations` (`Id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261101115406_AddMaterialReservations', '8.0.13');

COMMIT;

