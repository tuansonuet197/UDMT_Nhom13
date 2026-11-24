#ifndef DATA_CONTROL_H
#define DATA_CONTROL_H

#include <stdbool.h>
#include <stdint.h>
#include "esp_err.h"

#ifdef __cplusplus
extern "C" {
#endif

#define HLW_UART_NUM        1
#define HLW_RX_PIN          18
#define HLW_BAUD_RATE       4800
#define HLW_PACKET_LEN      24

#define HLW_VOLTAGE_COEFFICIENT   1.88f
#define HLW_CURRENT_COEFFICIENT   1.0f

#define HLW_CAL_TIME_US     (2 * 1000 * 1000)
#define HLW_CURRENT_ZERO_TH 0.1f
#define HLW_ENERGY_MAX_DT_US (1000 * 1000)


typedef struct {
    float voltage;
    float current;
    float active_power;
    float apparent_power;
    float power_factor;
    double energy_wh;
    uint32_t packet_count;
    bool valid;
} measurement_data_t;

esp_err_t data_control_start(void);
void data_control_get(measurement_data_t *data_out);
void data_control_reset_kwh(void);
void data_control_erase_nvs(void);

#ifdef __cplusplus
}
#endif

#endif
