-- ============================================================================
-- Phase 1 — Vendor normalisation: ADDITIVE SCHEMA ONLY.
--
-- WHY
--   bda_pump_master_tbl.vendor_name currently crams two facts into one string:
--   the contractor identity AND the pump's number within that contractor
--   (e.g. "Pump_2_Devanshi_contractor", "Pump_1.Modi Construction"), which has
--   already produced inconsistent separators/casing and two pumps that are
--   indistinguishable ("Dandotiya Construction Co." with no number at all).
--   This adds a proper vendor master + a per-contractor pump number.
--
-- RUNNING-HOURS SAFETY (the whole point of doing it this way)
--   Running hours key off pump_id ONLY. DailySummaryService reads just two
--   fields from bda_pump_master_tbl — is_active and row_insertion_date_time
--   (the latter caps day-start in ComputeDaySummary) — and never vendor_name.
--   Therefore this script:
--     * does NOT touch pump_id, row_insertion_date_time or is_active
--     * does NOT touch pump_status_log_tbl / pump_status_tbl /
--       pump_daily_summary_tbl in any way
--     * only ADDS a table and NULLable columns
--     * leaves vendor_name in place and still populated, so every existing
--       read path (dashboard, exports, push, map) keeps working unchanged
--       until the code is migrated in Phase 3.
--
--   Columns are appended (no AFTER clause) so MySQL 8 can use
--   ALGORITHM=INSTANT and avoid rewriting the table at all.
--
-- NOT IDEMPOTENT: MySQL has no "ADD COLUMN IF NOT EXISTS". Run once.
--   Guard check before running:
--     SELECT COUNT(*) FROM information_schema.columns
--     WHERE table_schema=DATABASE() AND table_name='bda_pump_master_tbl'
--       AND column_name='vendor_id';
--   Expect 0 before, 1 after.
--
-- ROLLBACK (safe — nothing else references these yet):
--   ALTER TABLE bda_pump_master_tbl
--     DROP FOREIGN KEY fk_pump_vendor,
--     DROP INDEX uq_vendor_pump_no,
--     DROP INDEX idx_pump_vendor,
--     DROP COLUMN legacy_vendor_name,
--     DROP COLUMN pump_no,
--     DROP COLUMN vendor_id;
--   DROP TABLE bda_vendor_master_tbl;
-- ============================================================================

-- 1. Contractor / vendor master.
CREATE TABLE IF NOT EXISTS `bda_vendor_master_tbl` (
    `vendor_id`               INT            NOT NULL AUTO_INCREMENT,
    `vendor_name`             VARCHAR(100)   NOT NULL,
    `contact_mobile`          VARCHAR(15)    NULL,
    `is_active`               TINYINT(1)     NOT NULL DEFAULT 1,
    `row_insertion_date_time` DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `row_updation_date_time`  DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`vendor_id`),
    -- One row per contractor; makes the Phase 2 backfill self-checking.
    UNIQUE KEY `uq_vendor_name` (`vendor_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. Pump master: link to vendor + per-contractor pump number.
--    All NULLable — existing rows are untouched and stay valid.
ALTER TABLE `bda_pump_master_tbl`
    ADD COLUMN `vendor_id`          INT          NULL,
    ADD COLUMN `pump_no`            INT          NULL,
    ADD COLUMN `legacy_vendor_name` VARCHAR(100) NULL;

-- 3. Integrity: a contractor can't have two "Pump 2".
--    (MySQL permits many NULLs in a UNIQUE index, so this is satisfied by the
--     current all-NULL state and only starts biting during the Phase 2 backfill.)
ALTER TABLE `bda_pump_master_tbl`
    ADD INDEX `idx_pump_vendor` (`vendor_id`),
    ADD UNIQUE KEY `uq_vendor_pump_no` (`vendor_id`, `pump_no`),
    ADD CONSTRAINT `fk_pump_vendor` FOREIGN KEY (`vendor_id`)
        REFERENCES `bda_vendor_master_tbl` (`vendor_id`) ON DELETE RESTRICT;
