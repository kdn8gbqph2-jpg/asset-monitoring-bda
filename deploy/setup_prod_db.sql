-- ============================================
-- Production DB Setup: bda_pump_prod
-- All 8 tables + seed data
-- ============================================

-- 1. bda_pump_master_tbl
CREATE TABLE IF NOT EXISTS `bda_pump_master_tbl` (
    `pump_id`                 INT            NOT NULL AUTO_INCREMENT,
    `vendor_name`             VARCHAR(100)   NOT NULL,
    `updated_by`              VARCHAR(100)   NULL,
    `category`                VARCHAR(50)    NULL,
    `is_active`               TINYINT(1)     NOT NULL DEFAULT 1,
    `row_action_count`        INT            NOT NULL DEFAULT 1,
    `row_insertion_date_time`  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `row_updation_date_time`   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`pump_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. bda_pump_location_tbl
CREATE TABLE IF NOT EXISTS `bda_pump_location_tbl` (
    `pump_id`                 INT            NOT NULL,
    `location_name`           VARCHAR(150)   NULL,
    `latitude`                DECIMAL(10,8)  NULL,
    `longitude`               DECIMAL(11,8)  NULL,
    `row_action_count`        INT            NOT NULL DEFAULT 1,
    `row_insertion_date_time`  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `row_updation_date_time`   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`pump_id`),
    CONSTRAINT `fk_pump_location` FOREIGN KEY (`pump_id`)
        REFERENCES `bda_pump_master_tbl` (`pump_id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. bda_user_master_tbl
CREATE TABLE IF NOT EXISTS `bda_user_master_tbl` (
    `user_id`                 INT            NOT NULL AUTO_INCREMENT,
    `name`                    VARCHAR(100)   NULL,
    `username`                VARCHAR(50)    NULL,
    `user_type`               ENUM('ADMIN','OPERATOR','JE') NULL,
    `password`                VARCHAR(255)   NULL,
    `mobile_number`           VARCHAR(15)    NULL,
    `is_active`               TINYINT(1)     NOT NULL DEFAULT 1,
    `row_insertion_date_time`  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `row_updation_date_time`   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. pump_status_tbl
CREATE TABLE IF NOT EXISTS `pump_status_tbl` (
    `pump_id`                 INT            NOT NULL,
    `status`                  ENUM('ON','OFF','MAINTENANCE') NOT NULL,
    `remarks`                 VARCHAR(255)   NULL,
    `current_start_time`      DATETIME       NULL,
    `current_end_time`        DATETIME       NULL,
    `last_run_time`           DATETIME       NULL,
    `updated_by`              VARCHAR(100)   NULL,
    `operator_mobile`         VARCHAR(15)    NULL,
    `je_mobile`               VARCHAR(15)    NULL,
    `row_action_count`        INT            NOT NULL DEFAULT 1,
    `row_insertion_date_time`  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `row_updation_date_time`   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`pump_id`),
    CONSTRAINT `fk_pump_status_pump` FOREIGN KEY (`pump_id`)
        REFERENCES `bda_pump_master_tbl` (`pump_id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 5. pump_status_log_tbl
CREATE TABLE IF NOT EXISTS `pump_status_log_tbl` (
    `log_id`                  INT            NOT NULL AUTO_INCREMENT,
    `pump_id`                 INT            NOT NULL,
    `location`                VARCHAR(150)   NULL,
    `old_status`              ENUM('ON','OFF','MAINTENANCE') NULL,
    `new_status`              ENUM('ON','OFF','MAINTENANCE') NOT NULL,
    `start_time`              DATETIME       NULL,
    `end_time`                DATETIME       NULL,
    `remarks`                 VARCHAR(255)   NULL,
    `updated_by`              VARCHAR(100)   NULL,
    `row_insertion_date_time`  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `row_updation_date_time`   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`log_id`),
    INDEX `idx_pump_status_log_pump` (`pump_id`),
    CONSTRAINT `fk_pump_status_log_pump` FOREIGN KEY (`pump_id`)
        REFERENCES `bda_pump_master_tbl` (`pump_id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 6. pump_daily_summary_tbl
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

-- 7. complaint_log_tbl
CREATE TABLE IF NOT EXISTS `complaint_log_tbl` (
    `complaint_id`            INT            NOT NULL AUTO_INCREMENT,
    `pump_id`                 INT            NOT NULL,
    `location`                VARCHAR(150)   NULL,
    `dashboard_status`        VARCHAR(20)    NULL,
    `actual_status`           VARCHAR(20)    NULL,
    `operator_name`           VARCHAR(100)   NULL,
    `operator_mobile`         VARCHAR(20)    NULL,
    `je_name`                 VARCHAR(100)   NULL,
    `je_mobile`               VARCHAR(20)    NULL,
    `complainant_name`        VARCHAR(100)   NULL,
    `complainant_mobile`      VARCHAR(20)    NULL,
    `status`                  VARCHAR(20)    NOT NULL DEFAULT 'OPEN',
    `remarks`                 VARCHAR(500)   NULL,
    `row_insertion_date_time`  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `row_updation_date_time`   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`complaint_id`),
    INDEX `idx_complaint_log_pump`   (`pump_id`),
    INDEX `idx_complaint_log_status` (`status`),
    CONSTRAINT `fk_complaint_log_pump` FOREIGN KEY (`pump_id`)
        REFERENCES `bda_pump_master_tbl` (`pump_id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 8. app_config_tbl
CREATE TABLE IF NOT EXISTS `app_config_tbl` (
    `config_id`               INT            NOT NULL AUTO_INCREMENT,
    `config_key`              VARCHAR(100)   NOT NULL,
    `config_value`            VARCHAR(2000)  NULL,
    `row_updation_date_time`   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`config_id`),
    UNIQUE INDEX `uq_app_config_key` (`config_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================
-- Seed: app_config defaults
-- ============================================
INSERT INTO app_config_tbl (config_key, config_value) VALUES
    ('complaint_whatsapp_send_to', 'Fixed Number'),
    ('complaint_whatsapp_number',  '919680111439'),
    ('complaint_message_template', '*PUMP COMPLAINT*\\n\\nPump ID: {pump_id}\\nVendor: {vendor}\\nLocation: {location}\\nDashboard Status: {status}\\nActual Status: {actual_status}\\n\\nOperator: {operator_name} ({operator_mobile})\\nJE: {je_name} ({je_mobile})\\n\\nComplainant: {complainant_name}\\nMobile: {complainant_mobile}'),
    ('complaint_drive_folder', '')
ON DUPLICATE KEY UPDATE config_key = config_key;

-- ============================================
-- Seed: Initial ADMIN user
-- Password: Admin@123 (plain text — app auto-upgrades to BCrypt on first login)
-- ============================================
INSERT INTO bda_user_master_tbl (name, username, user_type, password, mobile_number, is_active)
VALUES ('Administrator', 'admin', 'ADMIN', 'Admin@123', '0000000000', 1)
ON DUPLICATE KEY UPDATE user_id = user_id;

SELECT 'All tables created and seeded successfully!' AS result;
