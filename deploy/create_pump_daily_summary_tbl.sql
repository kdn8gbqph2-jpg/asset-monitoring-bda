-- pump_daily_summary_tbl
-- Pre-computed daily running summary for each pump, aggregated from pump_status_log_tbl.
-- Run this once on the database to create the table.

CREATE TABLE IF NOT EXISTS `pump_daily_summary_tbl` (
    `summary_id`              INT            NOT NULL AUTO_INCREMENT,
    `pump_id`                 INT            NOT NULL,
    `summary_date`            DATE           NOT NULL,
    `on_minutes`              INT            NOT NULL DEFAULT 0,
    `off_minutes`             INT            NOT NULL DEFAULT 0,
    `maintenance_minutes`     INT            NOT NULL DEFAULT 0,
    `status_change_count`     INT            NOT NULL DEFAULT 0,
    `first_status`            ENUM('ON','OFF','MAINTENANCE') NULL,
    `last_status`             ENUM('ON','OFF','MAINTENANCE') NULL,
    `row_insertion_date_time`  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `row_updation_date_time`   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    PRIMARY KEY (`summary_id`),
    UNIQUE KEY `uq_pump_daily_summary` (`pump_id`, `summary_date`),
    CONSTRAINT `fk_daily_summary_pump` FOREIGN KEY (`pump_id`)
        REFERENCES `bda_pump_master_tbl` (`pump_id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
