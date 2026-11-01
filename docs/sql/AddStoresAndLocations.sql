START TRANSACTION;

ALTER TABLE `StockMovements` ADD `StorageLocationId` int NOT NULL DEFAULT 0;

ALTER TABLE `MaterialReservations` ADD `StorageLocationId` int NOT NULL DEFAULT 0;

ALTER TABLE `MaterialIssues` ADD `StorageLocationId` int NOT NULL DEFAULT 0;

ALTER TABLE `InventoryOperations` ADD `DestinationStorageLocationId` int NULL;

ALTER TABLE `InventoryOperations` ADD `StorageLocationId` int NOT NULL DEFAULT 0;

ALTER TABLE `GoodsReceipts` ADD `StorageLocationId` int NOT NULL DEFAULT 0;

CREATE TABLE `Stores` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ProjectId` int NOT NULL,
    `Code` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_Stores` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Stores_Projects_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Projects` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `StorageLocations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `StoreId` int NOT NULL,
    `Code` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `Rack` varchar(60) CHARACTER SET utf8mb4 NOT NULL,
    `Bin` varchar(60) CHARACTER SET utf8mb4 NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_StorageLocations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_StorageLocations_Stores_StoreId` FOREIGN KEY (`StoreId`) REFERENCES `Stores` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

INSERT INTO `Stores` (`ProjectId`, `Code`, `Name`, `IsActive`) SELECT `Id`, 'MAIN', 'Main Store', 1 FROM `Projects`;
INSERT INTO `StorageLocations` (`StoreId`, `Code`, `Name`, `Rack`, `Bin`, `IsActive`) SELECT `Id`, 'DEFAULT', 'Default Location', '', '', 1 FROM `Stores` WHERE `Code` = 'MAIN';
UPDATE `StockMovements` t JOIN `Stores` s ON s.`ProjectId` = t.`ProjectId` AND s.`Code` = 'MAIN' JOIN `StorageLocations` l ON l.`StoreId` = s.`Id` AND l.`Code` = 'DEFAULT' SET t.`StorageLocationId` = l.`Id`;
UPDATE `MaterialReservations` t JOIN `Stores` s ON s.`ProjectId` = t.`ProjectId` AND s.`Code` = 'MAIN' JOIN `StorageLocations` l ON l.`StoreId` = s.`Id` AND l.`Code` = 'DEFAULT' SET t.`StorageLocationId` = l.`Id`;
UPDATE `MaterialIssues` t JOIN `Stores` s ON s.`ProjectId` = t.`ProjectId` AND s.`Code` = 'MAIN' JOIN `StorageLocations` l ON l.`StoreId` = s.`Id` AND l.`Code` = 'DEFAULT' SET t.`StorageLocationId` = l.`Id`;
UPDATE `InventoryOperations` t JOIN `Stores` s ON s.`ProjectId` = t.`ProjectId` AND s.`Code` = 'MAIN' JOIN `StorageLocations` l ON l.`StoreId` = s.`Id` AND l.`Code` = 'DEFAULT' SET t.`StorageLocationId` = l.`Id`;
UPDATE `GoodsReceipts` g JOIN `PurchaseOrders` o ON o.`Id` = g.`PurchaseOrderId` JOIN `MaterialRequests` r ON r.`Id` = o.`MaterialRequestId` JOIN `Stores` s ON s.`ProjectId` = r.`ProjectId` AND s.`Code` = 'MAIN' JOIN `StorageLocations` l ON l.`StoreId` = s.`Id` AND l.`Code` = 'DEFAULT' SET g.`StorageLocationId` = l.`Id`;
UPDATE `InventoryOperations` o JOIN `Stores` s ON s.`ProjectId` = o.`DestinationProjectId` AND s.`Code` = 'MAIN' JOIN `StorageLocations` l ON l.`StoreId` = s.`Id` AND l.`Code` = 'DEFAULT' SET o.`DestinationStorageLocationId` = l.`Id` WHERE o.`DestinationProjectId` IS NOT NULL;

CREATE INDEX `IX_StockMovements_StorageLocationId_MaterialId` ON `StockMovements` (`StorageLocationId`, `MaterialId`);

CREATE INDEX `IX_MaterialReservations_StorageLocationId_MaterialId` ON `MaterialReservations` (`StorageLocationId`, `MaterialId`);

CREATE INDEX `IX_MaterialIssues_StorageLocationId` ON `MaterialIssues` (`StorageLocationId`);

CREATE INDEX `IX_InventoryOperations_DestinationStorageLocationId` ON `InventoryOperations` (`DestinationStorageLocationId`);

CREATE INDEX `IX_InventoryOperations_StorageLocationId` ON `InventoryOperations` (`StorageLocationId`);

CREATE INDEX `IX_GoodsReceipts_StorageLocationId` ON `GoodsReceipts` (`StorageLocationId`);

CREATE UNIQUE INDEX `IX_StorageLocations_StoreId_Code` ON `StorageLocations` (`StoreId`, `Code`);

CREATE UNIQUE INDEX `IX_Stores_ProjectId_Code` ON `Stores` (`ProjectId`, `Code`);

ALTER TABLE `GoodsReceipts` ADD CONSTRAINT `FK_GoodsReceipts_StorageLocations_StorageLocationId` FOREIGN KEY (`StorageLocationId`) REFERENCES `StorageLocations` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `InventoryOperations` ADD CONSTRAINT `FK_InventoryOperations_StorageLocations_DestinationStorageLocat~` FOREIGN KEY (`DestinationStorageLocationId`) REFERENCES `StorageLocations` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `InventoryOperations` ADD CONSTRAINT `FK_InventoryOperations_StorageLocations_StorageLocationId` FOREIGN KEY (`StorageLocationId`) REFERENCES `StorageLocations` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `MaterialIssues` ADD CONSTRAINT `FK_MaterialIssues_StorageLocations_StorageLocationId` FOREIGN KEY (`StorageLocationId`) REFERENCES `StorageLocations` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `MaterialReservations` ADD CONSTRAINT `FK_MaterialReservations_StorageLocations_StorageLocationId` FOREIGN KEY (`StorageLocationId`) REFERENCES `StorageLocations` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `StockMovements` ADD CONSTRAINT `FK_StockMovements_StorageLocations_StorageLocationId` FOREIGN KEY (`StorageLocationId`) REFERENCES `StorageLocations` (`Id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261101122858_AddStoresAndLocations', '8.0.13');

COMMIT;

