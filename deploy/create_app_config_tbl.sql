-- App Config table
CREATE TABLE IF NOT EXISTS app_config_tbl (
    config_id               INT             NOT NULL AUTO_INCREMENT,
    config_key              VARCHAR(100)    NOT NULL,
    config_value            VARCHAR(2000)   NULL,
    row_updation_date_time  DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    PRIMARY KEY (config_id),
    UNIQUE INDEX uq_app_config_key (config_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Default settings
INSERT INTO app_config_tbl (config_key, config_value) VALUES
    ('complaint_whatsapp_send_to', 'Fixed Number'),
    ('complaint_whatsapp_number',  '919680111439'),
    ('complaint_message_template', '*PUMP COMPLAINT*\n\nPump ID: {pump_id}\nVendor: {vendor}\nLocation: {location}\nDashboard Status: {status}\nActual Status: {actual_status}\n\nOperator: {operator_name} ({operator_mobile})\nJE: {je_name} ({je_mobile})\n\nComplainant: {complainant_name}\nMobile: {complainant_mobile}'),
    ('complaint_drive_folder', '')
ON DUPLICATE KEY UPDATE config_key = config_key;
