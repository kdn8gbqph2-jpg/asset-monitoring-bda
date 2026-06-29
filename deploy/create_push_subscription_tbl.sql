-- Web Push subscriptions (one row per device/browser per user).
-- Run once on the production DB before deploying the PWA/push build.
CREATE TABLE IF NOT EXISTS `push_subscription_tbl` (
    `id`                       INT            NOT NULL AUTO_INCREMENT,
    `user_id`                  INT            NULL,
    `mobile`                   VARCHAR(15)    NULL,
    `user_type`                VARCHAR(20)    NULL,
    `endpoint`                 VARCHAR(500)   NOT NULL,
    `p256dh`                   VARCHAR(255)   NOT NULL,
    `auth`                     VARCHAR(255)   NOT NULL,
    `row_insertion_date_time`  DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `row_updation_date_time`   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uq_push_endpoint` (`endpoint`),
    INDEX `idx_push_user` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
