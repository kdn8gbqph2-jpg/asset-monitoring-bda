-- ============================================================================
-- Data repair: rebuild corrupted pump_daily_summary rows
-- ============================================================================
-- The Running Summary table (pump_daily_summary_tbl) was computed by the OLD
-- forward-chain algorithm, which credited phantom ON time on days a pump was
-- actually OFF (e.g. Pump 7 ~20h "running" on 23-Jun while OFF all day).
--
-- The fix recomputes each day with the new anchor-based algorithm. This script
-- simply DELETES the affected summary rows; the application then rebuilds them
-- automatically on next startup:
--   • DailySummaryBackgroundService.BackfillIncompleteDays() rebuilds any of the
--     last 30 days whose summary rows are missing, and
--   • SafeBuild() rebuilds today + yesterday.
-- Both now run the corrected ComputeDaySummary, so the rebuilt numbers match the
-- detail log.
--
-- ─────────────────────────────────────────────────────────────────────────────
-- ORDER OF OPERATIONS (important):
--   1. Deploy the fixed application build FIRST (the one with the anchor-based
--      DailySummaryService). If you delete these rows while the OLD build is
--      still running, it will just rebuild them wrong again.
--   2. Run this script.
--   3. Restart the application so the startup backfill runs.
--   4. Verify in the Running Summary UI and with diagnose_pump_log_chain.sql.
--
-- SCOPE: only the last ~30 days can be auto-rebuilt (the backfill window). The
-- range below is set to the last 30 days, which covers all of June 2026. If you
-- need an older month repaired, tell the developer to add an explicit
-- range-rebuild — deleting older rows would otherwise leave permanent gaps.
-- ─────────────────────────────────────────────────────────────────────────────

-- Optional safety: back up the rows you are about to delete.
-- CREATE TABLE pump_daily_summary_bak_20260624 AS
--   SELECT * FROM pump_daily_summary_tbl
--   WHERE summary_date >= (CURDATE() - INTERVAL 30 DAY);

-- Preview what will be deleted (run this first):
SELECT summary_date, COUNT(*) AS rows_to_delete
FROM pump_daily_summary_tbl
WHERE summary_date >= (CURDATE() - INTERVAL 30 DAY)
GROUP BY summary_date
ORDER BY summary_date;

-- The actual repair:
DELETE FROM pump_daily_summary_tbl
WHERE summary_date >= (CURDATE() - INTERVAL 30 DAY);

-- After restart, confirm every day was rebuilt and totals look sane
-- (on + off + maintenance should be ~1440 for each completed day):
-- SELECT summary_date, pump_id, on_minutes, off_minutes, maintenance_minutes,
--        (on_minutes + off_minutes + maintenance_minutes) AS total_minutes
-- FROM pump_daily_summary_tbl
-- WHERE summary_date >= (CURDATE() - INTERVAL 30 DAY)
-- ORDER BY summary_date, pump_id;
