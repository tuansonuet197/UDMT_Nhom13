#include <math.h>
#include <string.h>
#include "data_screen.h"
#include "driver/uart.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/semphr.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "nvs.h"
#include "nvs_flash.h"

static const char *TAG = "HLW8032";

#define NVS_NAMESPACE       "energy_app"
#define NVS_KEY_KWH         "kwh"
#define NVS_SAVE_PERIOD_US  (60LL * 1000 * 1000)
#define NVS_SAVE_DELTA_KWH  0.001

static measurement_data_t data;
static SemaphoreHandle_t s_lock;
static SemaphoreHandle_t s_nvs_lock;
static nvs_handle_t s_nvs_handle;
static bool s_nvs_ready;
static double s_saved_kwh;

static float   raw_current;
static float   curr_offset;
static float   curr_cal = 0.36;
static double  cal_sum;
static uint32_t cal_count;
static int64_t cal_start_us = -1;
static bool    calibrated;

static int64_t s_last_energy_us;

static void nvs_load_state(void)
{
    data.energy_wh = 0.0;

    if (nvs_open(NVS_NAMESPACE, NVS_READWRITE, &s_nvs_handle) != ESP_OK) {
        s_nvs_ready = false;
        return;
    }
    s_nvs_ready = true;

    double kwh = 0.0;
    size_t sz = sizeof(kwh);
    if (nvs_get_blob(s_nvs_handle, NVS_KEY_KWH, &kwh, &sz) == ESP_OK &&
        sz == sizeof(kwh) && isfinite(kwh) && kwh >= 0.0) {
        data.energy_wh = kwh * 1000.0;
        s_saved_kwh = kwh;
    }
}

static void nvs_save_kwh(double kwh)
{
    if (!s_nvs_ready) {
        return;
    }
    xSemaphoreTake(s_nvs_lock, portMAX_DELAY);
    if (nvs_set_blob(s_nvs_handle, NVS_KEY_KWH, &kwh, sizeof(kwh)) == ESP_OK) {
        if (nvs_commit(s_nvs_handle) == ESP_OK) {
            s_saved_kwh = kwh;
        }
    }
    xSemaphoreGive(s_nvs_lock);
}

static uint32_t get_data(const uint8_t *p)
{
    return ((uint32_t)p[0] << 16) | ((uint32_t)p[1] << 8) | p[2];
}

static bool get_state_REG(uint8_t b)
{
    return b == 0x55 || b == 0xAA || (b & 0xF0) == 0xF0;
}

static bool process_packet(const uint8_t *buf)
{
    /// Check REG
    if (buf[1] != 0x5A) {
        return false;
    }
    ///

    /// Checksum cua HLW8032 la 8 bit thap cua tong data = byte checksum
    uint8_t checksum = 0;
    for (int i = 2; i < 23; i++) {
        checksum += buf[i];
    }
    if (checksum != buf[23]) {
        return false;
    }
    ///

    /// Check state REG
    uint8_t state = buf[0];
    if (state == 0xAA) {
        return true;
    }

    bool is_fx = ((state & 0xF0) == 0xF0);
    if (is_fx && (state & (1 << 0))) {
        return true;
    }
    ///

    bool volt_ovf  = is_fx && (state & (1 << 3));
    bool curr_ovf  = is_fx && (state & (1 << 2));
    bool power_ovf = is_fx && (state & (1 << 1));

    uint8_t update = buf[20];
    bool volt_ready  = (update & (1 << 6)) != 0;
    bool curr_ready  = (update & (1 << 5)) != 0;
    bool power_ready = (update & (1 << 4)) != 0;

    uint32_t volt_param  = get_data(&buf[2]);
    uint32_t volt_reg    = get_data(&buf[5]);
    uint32_t curr_param  = get_data(&buf[8]);
    uint32_t curr_reg    = get_data(&buf[11]);
    uint32_t power_param = get_data(&buf[14]);
    uint32_t power_reg   = get_data(&buf[17]);

    xSemaphoreTake(s_lock, portMAX_DELAY);


    if (volt_ovf) {
        data.voltage = 0.0f;
    } else if (volt_ready && volt_param > 0 && volt_reg > 0) {
        data.voltage = ((float)volt_param / (float)volt_reg) * HLW_VOLTAGE_COEFFICIENT;
    }

    bool curr_new = false;
    if (curr_ovf) {
        raw_current = 0.0f;
        curr_new = true;
    } else if (curr_ready && curr_param > 0 && curr_reg > 0) {
        raw_current = ((float)curr_param / (float)curr_reg) * HLW_CURRENT_COEFFICIENT;
        curr_new = true;
    }

    if (!calibrated) {
        int64_t now = esp_timer_get_time();
        if (cal_start_us < 0) {
            cal_start_us = now;
        }
        if (curr_new) {
            cal_sum += raw_current;
            cal_count++;
        }
        if (now - cal_start_us >= HLW_CAL_TIME_US) {
            curr_offset = (cal_count > 0) ? (float)(cal_sum / cal_count) : 0.0f;
            calibrated = true;
            ESP_LOGI(TAG, "Hieu chinh I xong: offset=%.4f A (%u mau)",
                     curr_offset, (unsigned)cal_count);
        }
        data.current = 0.0f;
    } else if (curr_new) {
        data.current = raw_current - curr_offset;
        if (data.current <= HLW_CURRENT_ZERO_TH) {
            data.current = 0.0f;
        } else {
            data.current += curr_cal;
        }
    }

    if (power_ovf || curr_ovf) {
        data.active_power = 0.0f;
    } else if (power_ready && power_param > 0 && power_reg > 0) {
        data.active_power = ((float)power_param / (float)power_reg) * HLW_VOLTAGE_COEFFICIENT * HLW_CURRENT_COEFFICIENT;
    }

    if (data.current <= 0.0f) {
        data.active_power = 0.0f;
        data.voltage = 0.0f;
    }

    int64_t now_us = esp_timer_get_time();
    if (s_last_energy_us > 0) {
        int64_t dt_us = now_us - s_last_energy_us;
        if (dt_us > HLW_ENERGY_MAX_DT_US) {
            dt_us = HLW_ENERGY_MAX_DT_US;
        }
        data.energy_wh += (double)data.active_power * ((double)dt_us / 3600e6);
    }
    s_last_energy_us = now_us;

    float apparent = data.voltage * data.current;
    float pf = 0.0f;
    if (apparent > 0.0f) {
        pf = data.active_power / apparent;
        if (pf > 1.0f) pf = 1.0f;
        if (pf < 0.0f) pf = 0.0f;
    }

    data.apparent_power = apparent;
    data.power_factor = pf;
    data.packet_count++;
    data.valid = true;

    xSemaphoreGive(s_lock);
    return true;
}

static void hlw_task(void *arg)
{
    uint8_t rx[64];
    uint8_t buf[HLW_PACKET_LEN];
    int idx = 0;
    int64_t last_save = esp_timer_get_time();

    while (1) {
        int len = uart_read_bytes(HLW_UART_NUM, rx, sizeof(rx), pdMS_TO_TICKS(50));
        for (int n = 0; n < len; n++) {
            uint8_t b = rx[n];

            if (idx == 0 && !get_state_REG(b)) {
                continue;
            }
            buf[idx++] = b;

            if (idx < HLW_PACKET_LEN) {
                continue;
            }

            if (process_packet(buf)) {
                idx = 0;
                int64_t now = esp_timer_get_time();
                if (now - last_save >= NVS_SAVE_PERIOD_US) {
                    last_save = now;
                    measurement_data_t d;
                    data_control_get(&d);
                    double kwh = d.energy_wh / 1000.0;
                    if (fabs(kwh - s_saved_kwh) >= NVS_SAVE_DELTA_KWH) {
                        nvs_save_kwh(kwh);
                    }
                }
            } else {
                int start = 1;
                while (start < HLW_PACKET_LEN && !get_state_REG(buf[start])) {
                    start++;
                }
                idx = HLW_PACKET_LEN - start;
                if (idx > 0) {
                    memmove(buf, &buf[start], idx);
                } else {
                    idx = 0;
                }
            }
        }
    }
}

void data_control_get(measurement_data_t *data_out)
{
    xSemaphoreTake(s_lock, portMAX_DELAY);
    *data_out = data;
    xSemaphoreGive(s_lock);
}

void data_control_reset_kwh(void)
{
    xSemaphoreTake(s_lock, portMAX_DELAY);
    data.energy_wh = 0.0;
    xSemaphoreGive(s_lock);

    nvs_save_kwh(0.0);
}

void data_control_erase_nvs(void)
{
    xSemaphoreTake(s_nvs_lock, portMAX_DELAY);
    if (s_nvs_ready) {
        nvs_close(s_nvs_handle);
        s_nvs_ready = false;
    }
    ESP_ERROR_CHECK(nvs_flash_erase());
    xSemaphoreGive(s_nvs_lock);
}

esp_err_t data_control_start(void)
{
    s_lock = xSemaphoreCreateMutex();
    s_nvs_lock = xSemaphoreCreateMutex();
    if (!s_lock || !s_nvs_lock) {
        return ESP_ERR_NO_MEM;
    }

    nvs_load_state();

    const uart_config_t cfg = {
        .baud_rate = HLW_BAUD_RATE,
        .data_bits = UART_DATA_8_BITS,
        .parity = UART_PARITY_EVEN,
        .stop_bits = UART_STOP_BITS_1,
        .flow_ctrl = UART_HW_FLOWCTRL_DISABLE,
        .source_clk = UART_SCLK_DEFAULT,
    };
    ESP_ERROR_CHECK(uart_driver_install(HLW_UART_NUM, 512, 0, 0, NULL, 0));
    ESP_ERROR_CHECK(uart_param_config(HLW_UART_NUM, &cfg));
    ESP_ERROR_CHECK(uart_set_pin(HLW_UART_NUM, UART_PIN_NO_CHANGE, HLW_RX_PIN,
                                 UART_PIN_NO_CHANGE, UART_PIN_NO_CHANGE));

    xTaskCreate(hlw_task, "hlw8032", 4096, NULL, 5, NULL);
    ESP_LOGI(TAG, "UART%d started, RX=GPIO%d, %d 8E1 (kwh=%.3f)",
             HLW_UART_NUM, HLW_RX_PIN, HLW_BAUD_RATE,
             data.energy_wh / 1000.0);
    return ESP_OK;
}
