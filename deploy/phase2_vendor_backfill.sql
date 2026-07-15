-- ============================================================================
-- Phase 2 — Vendor backfill (data only; run AFTER phase1_vendor_normalisation.sql)
--
-- Populates bda_vendor_master_tbl and sets vendor_id / pump_no /
-- legacy_vendor_name on the 41 production pumps. Mapping was reviewed by hand
-- against all 44 rows (see phase2_pump_mapping.csv).
--
-- SKIPPED ON PURPOSE (vendor_id stays NULL — they still render via the
-- vendor_name fallback): pump 1 & 2 ("QA test") and pump 3 (empty vendor_name),
-- all inactive test rows.
--
-- RUNNING-HOURS SAFETY
--   * UPDATE-only. No INSERT/DELETE on bda_pump_master_tbl, so pump_id and
--     row_insertion_date_time (which caps day-start in ComputeDaySummary) are
--     untouched. is_active is not in any SET list.
--   * row_updation_date_time is explicitly re-assigned to itself so the
--     ON UPDATE CURRENT_TIMESTAMP trigger cannot shift it (it feeds the
--     "Last Updated" display for pumps with no status entry).
--   * pump_status_log_tbl / pump_status_tbl / pump_daily_summary_tbl: untouched.
--   * vendor_name is left exactly as-is, so all current read paths keep working
--     until Phase 3 switches the display over.
--
-- Re-runnable: INSERT IGNORE + an idempotent UPDATE.
--
-- ROLLBACK:
--   UPDATE bda_pump_master_tbl
--     SET vendor_id=NULL, pump_no=NULL, legacy_vendor_name=NULL,
--         row_updation_date_time=row_updation_date_time;
--   DELETE FROM bda_vendor_master_tbl;
-- ============================================================================

-- 1. Contractors (vendor_name is UNIQUE, so re-runs are no-ops).
INSERT IGNORE INTO `bda_vendor_master_tbl` (`vendor_name`) VALUES
    ('Dandotiya Construction Co.'),
    ('Devanshi Contractor'),
    ('Devendra Contractor'),
    ('Kiran Construction'),
    ('Krishna Construction'),
    ('Modi Construction'),
    ('PC Contractor'),
    ('VijayBhan'),
    ('VK Construction');

-- 2. Link each pump to its contractor + per-contractor number.
--    Only the pump_ids listed below are touched; anything absent (1, 2, 3)
--    keeps vendor_id = NULL.
UPDATE `bda_pump_master_tbl` p
JOIN (
              SELECT  5 AS pump_id, 'Dandotiya Construction Co.' AS vname, 1 AS pno
    UNION ALL SELECT  6, 'Dandotiya Construction Co.', 2
    UNION ALL SELECT  7, 'Devanshi Contractor',        1
    UNION ALL SELECT  8, 'Devanshi Contractor',        2
    UNION ALL SELECT  9, 'Devanshi Contractor',        3
    UNION ALL SELECT 14, 'Devendra Contractor',        1
    UNION ALL SELECT 15, 'Devendra Contractor',        2
    UNION ALL SELECT 17, 'Devendra Contractor',        3
    UNION ALL SELECT 18, 'Devendra Contractor',        4
    UNION ALL SELECT 19, 'Devendra Contractor',        5
    UNION ALL SELECT 20, 'Devendra Contractor',        6
    UNION ALL SELECT 29, 'Devendra Contractor',        7
    UNION ALL SELECT 35, 'Kiran Construction',         1
    UNION ALL SELECT 40, 'Kiran Construction',         2
    UNION ALL SELECT 41, 'Kiran Construction',         3
    UNION ALL SELECT 44, 'Kiran Construction',         4
    UNION ALL SELECT 22, 'Krishna Construction',       1
    UNION ALL SELECT 23, 'Krishna Construction',       2
    UNION ALL SELECT 32, 'Krishna Construction',       3
    UNION ALL SELECT 33, 'Krishna Construction',       4
    UNION ALL SELECT 36, 'Krishna Construction',       5
    UNION ALL SELECT 37, 'Krishna Construction',       6
    UNION ALL SELECT 10, 'Modi Construction',          1
    UNION ALL SELECT 11, 'Modi Construction',          2
    UNION ALL SELECT 12, 'Modi Construction',          3
    UNION ALL SELECT 13, 'Modi Construction',          4
    UNION ALL SELECT 16, 'VijayBhan',                  1
    UNION ALL SELECT 21, 'VijayBhan',                  2
    UNION ALL SELECT 24, 'VijayBhan',                  3
    UNION ALL SELECT 30, 'VijayBhan',                  4
    UNION ALL SELECT 31, 'VijayBhan',                  5
    UNION ALL SELECT 34, 'VijayBhan',                  6
    UNION ALL SELECT 42, 'VijayBhan',                  7
    UNION ALL SELECT 43, 'VijayBhan',                  8
    UNION ALL SELECT 25, 'VK Construction',            1
    UNION ALL SELECT 26, 'VK Construction',            2
    UNION ALL SELECT 27, 'VK Construction',            3
    UNION ALL SELECT 28, 'VK Construction',            4
    UNION ALL SELECT 38, 'VK Construction',            5
    UNION ALL SELECT 39, 'VK Construction',            6
    UNION ALL SELECT  4, 'PC Contractor',              1
) m ON m.pump_id = p.pump_id
JOIN `bda_vendor_master_tbl` v ON v.vendor_name = m.vname
SET p.vendor_id              = v.vendor_id,
    p.pump_no                = m.pno,
    p.legacy_vendor_name     = p.vendor_name,
    p.row_updation_date_time = p.row_updation_date_time;  -- pin: don't let ON UPDATE shift it
