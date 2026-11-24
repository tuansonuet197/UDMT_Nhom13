#include "driver/gpio.h"
#include "esp_log.h"
#include "esp_system.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "nvs_flash.h"
#include "data_screen.h"
#include "ui_control.h"
#include "data_pc.h"

#define BOOT_BTN_GPIO      GPIO_NUM_0
#define BOOT_HOLD_TIME_US  (3 * 1000 * 1000)

static const char *TAG = "app_main";

static void boot_btn_task(void *arg)
{
    const gpio_config_t btn_cfg = {
        .pin_bit_mask = 1ULL << BOOT_BTN_GPIO,
        .mode = GPIO_MODE_INPUT,
        .pull_up_en = GPIO_PULLUP_ENABLE,
        .pull_down_en = GPIO_PULLDOWN_DISABLE,
        .intr_type = GPIO_INTR_DISABLE,
    };
    ESP_ERROR_CHECK(gpio_config(&btn_cfg));

    int64_t press_start_us = -1;
    while (1) {
        if (gpio_get_level(BOOT_BTN_GPIO) == 0) {
            int64_t now = esp_timer_get_time();
            if (press_start_us < 0) {
                press_start_us = now;
            } else if (now - press_start_us >= BOOT_HOLD_TIME_US) {
                ESP_LOGW(TAG, "BOOT held 3s: erasing NVS and restarting...");
                data_control_erase_nvs();
                esp_restart();
            }
        } else {
            press_start_us = -1;
        }
        vTaskDelay(pdMS_TO_TICKS(50));
    }
}

void app_main(void)
{
    esp_err_t ret = nvs_flash_init();
    if (ret == ESP_ERR_NVS_NO_FREE_PAGES || ret == ESP_ERR_NVS_NEW_VERSION_FOUND) {
        ESP_ERROR_CHECK(nvs_flash_erase());
        ret = nvs_flash_init();
    }
    ESP_ERROR_CHECK(ret);

    ESP_ERROR_CHECK(data_control_start());
    ESP_ERROR_CHECK(pc_comm_start());
    ESP_ERROR_CHECK(display_start());
    xTaskCreate(boot_btn_task, "boot_btn", 4096, NULL, 3, NULL);
}
