-- ============================================================================
-- Pump status-log chain diagnostics  (READ-ONLY — safe to run on production)
--
-- Purpose: prove/quantify the log-chain corruption that made the Running Summary
-- credit ON time on days a pump was actually OFF.
--
-- Background: each row in pump_status_log_tbl means "the pump was in OLD_STATUS
-- from start_time to end_time, then became NEW_STATUS". In a healthy chain, for
-- each pump (ordered by time):
--     row.old_status  == previous row.new_status   AND
--     row.start_time  == previous row.end_time
-- A break in either invariant means a transition was lost (the removed 6-hour
-- "duplicate" guard dropped the log row while still flipping the live status).
--
-- All timestamps are stored in UTC. Add INTERVAL 5 HOUR 30 MINUTE for IST when
-- eyeballing values against the dashboard.
-- ============================================================================

-- 1) Every broken-chain row, with the reason.
WITH ordered AS (
    SELECT
        log_id, pump_id, old_status, new_status, start_time, end_time,
        LAG(new_status) OVER w AS prev_new_status,
        LAG(end_time)   OVER w AS prev_end_time
    FROM pump_status_log_tbl
    WINDOW w AS (
        PARTITION BY pump_id
        ORDER BY COALESCE(start_time, end_time, row_insertion_date_time), log_id
    )
)
SELECT
    pump_id, log_id, old_status, new_status, start_time, end_time,
    prev_new_status, prev_end_time,
    CASE
        WHEN prev_new_status IS NOT NULL AND old_status <> prev_new_status THEN 'OLD_STATUS_MISMATCH'
        WHEN prev_end_time   IS NOT NULL AND start_time <> prev_end_time    THEN 'TIME_GAP'
        ELSE 'OK'
    END AS chain_issue
FROM ordered
WHERE (prev_new_status IS NOT NULL AND old_status <> prev_new_status)
   OR (prev_end_time   IS NOT NULL AND start_time  <> prev_end_time)
ORDER BY pump_id, COALESCE(start_time, end_time), log_id;

-- 2) Per-pump count of chain breaks (worst offenders first).
WITH ordered AS (
    SELECT
        pump_id, old_status, start_time,
        LAG(new_status) OVER w AS prev_new_status,
        LAG(end_time)   OVER w AS prev_end_time
    FROM pump_status_log_tbl
    WINDOW w AS (
        PARTITION BY pump_id
        ORDER BY COALESCE(start_time, end_time, row_insertion_date_time), log_id
    )
)
SELECT
    pump_id,
    SUM(prev_new_status IS NOT NULL AND old_status <> prev_new_status) AS old_status_mismatches,
    SUM(prev_end_time   IS NOT NULL AND start_time  <> prev_end_time)  AS time_gaps
FROM ordered
GROUP BY pump_id
HAVING old_status_mismatches > 0 OR time_gaps > 0
ORDER BY old_status_mismatches DESC, time_gaps DESC;

-- 3) Consecutive identical transitions (e.g. two OFF→ON in a row) — the exact
--    signature of a suppressed transition (the report's logs #47 / #48).
WITH ordered AS (
    SELECT
        pump_id, log_id, old_status, new_status, start_time, end_time,
        LAG(old_status) OVER w AS prev_old,
        LAG(new_status) OVER w AS prev_new
    FROM pump_status_log_tbl
    WINDOW w AS (
        PARTITION BY pump_id
        ORDER BY COALESCE(start_time, end_time, row_insertion_date_time), log_id
    )
)
SELECT pump_id, log_id, old_status, new_status, start_time, end_time
FROM ordered
WHERE old_status = prev_old AND new_status = prev_new
ORDER BY pump_id, COALESCE(start_time, end_time), log_id;

-- 4) Suspiciously long single-status spans (> 24h in one row) — phantom periods
--    usually surface here.
SELECT
    pump_id, log_id, old_status, new_status, start_time, end_time,
    TIMESTAMPDIFF(MINUTE, start_time, end_time) AS minutes_in_old_status
FROM pump_status_log_tbl
WHERE start_time IS NOT NULL AND end_time IS NOT NULL
  AND TIMESTAMPDIFF(MINUTE, start_time, end_time) > 24 * 60
ORDER BY minutes_in_old_status DESC;
