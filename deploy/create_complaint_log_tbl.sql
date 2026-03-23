-- Complaint Log table
CREATE TABLE IF NOT EXISTS complaint_log_tbl (
    complaint_id        INT             NOT NULL AUTO_INCREMENT,
    pump_id             INT             NOT NULL,
    location            VARCHAR(150)    NULL,
    dashboard_status    VARCHAR(20)     NULL,
    actual_status       VARCHAR(20)     NULL,
    operator_name       VARCHAR(100)    NULL,
    operator_mobile     VARCHAR(20)     NULL,
    je_name             VARCHAR(100)    NULL,
    je_mobile           VARCHAR(20)     NULL,
    complainant_name    VARCHAR(100)    NULL,
    complainant_mobile  VARCHAR(20)     NULL,
    status              VARCHAR(20)     NOT NULL DEFAULT 'OPEN',
    row_insertion_date_time DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    row_updation_date_time  DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    PRIMARY KEY (complaint_id),
    INDEX idx_complaint_log_pump   (pump_id),
    INDEX idx_complaint_log_status (status),

    CONSTRAINT fk_complaint_log_pump
        FOREIGN KEY (pump_id) REFERENCES bda_pump_master_tbl(pump_id)
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
