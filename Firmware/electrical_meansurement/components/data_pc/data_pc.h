#ifndef PC_COMM_H
#define PC_COMM_H

#include "esp_err.h"

#ifdef __cplusplus
extern "C" {
#endif

#define PC_UART_NUM         UART_NUM_0
#define PC_UART_BAUD_RATE   115200
#define PC_SEND_PERIOD_US   (200 * 1000)

esp_err_t pc_comm_start(void);

#ifdef __cplusplus
}
#endif

#endif
