#include <stdio.h>
#include "ui_control.h"
#include "data_screen.h"

#include "driver/gpio.h"
#include "driver/spi_master.h"
#include "esp_lcd_panel_io.h"
#include "esp_lcd_panel_ops.h"
#include "esp_lcd_ili9341.h"
#include "esp_heap_caps.h"
#include "esp_log.h"
#include "esp_system.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#include "lvgl.h"
#include "GUI.h"

static const char *TAG = "display";

#define LCD_HOST SPI2_HOST
#define PIN_LCD_SCLK 12
#define PIN_LCD_MOSI 11
#define PIN_LCD_RST 10
#define PIN_LCD_DC 9
#define PIN_LCD_CS 8
#define PIN_LCD_BL 7
#define LCD_BL_ON_LEVEL 1
#define LCD_SPI_HZ (40 * 1000 * 1000)

#define LCD_H_RES 320
#define LCD_V_RES 240
#define LCD_MIRROR_X true
#define LCD_MIRROR_Y true

#define LVGL_BUF_LINES 30

static esp_lcd_panel_handle_t s_panel;

static bool on_color_trans_done(esp_lcd_panel_io_handle_t io, esp_lcd_panel_io_event_data_t *edata, void *ctx)
{
    lv_display_flush_ready((lv_display_t *)ctx);
    return false;
}

static void flush_cb(lv_display_t *disp, const lv_area_t *area, uint8_t *px_map)
{
    uint32_t w = lv_area_get_width(area);
    uint32_t h = lv_area_get_height(area);
    lv_draw_sw_rgb565_swap(px_map, w * h);
    esp_lcd_panel_draw_bitmap(s_panel, area->x1, area->y1, area->x2 + 1, area->y2 + 1, px_map);
}

static uint32_t tick_cb(void)
{
    return (uint32_t)(esp_timer_get_time() / 1000);
}

void HAL_init(void)
{
    gpio_config_t bl = {
        .mode = GPIO_MODE_OUTPUT,
        .pin_bit_mask = 1ULL << PIN_LCD_BL,
    };
    gpio_config(&bl);
    gpio_set_level(PIN_LCD_BL, !LCD_BL_ON_LEVEL);

    spi_bus_config_t buscfg = {
        .sclk_io_num = PIN_LCD_SCLK,
        .mosi_io_num = PIN_LCD_MOSI,
        .miso_io_num = -1,
        .quadwp_io_num = -1,
        .quadhd_io_num = -1,
        .max_transfer_sz = LCD_H_RES * LVGL_BUF_LINES * sizeof(uint16_t),
    };
    ESP_ERROR_CHECK(spi_bus_initialize(LCD_HOST, &buscfg, SPI_DMA_CH_AUTO));

    lv_display_t *disp = lv_display_create(LCD_H_RES, LCD_V_RES);

    esp_lcd_panel_io_handle_t io = NULL;
    esp_lcd_panel_io_spi_config_t io_cfg = {
        .dc_gpio_num = PIN_LCD_DC,
        .cs_gpio_num = PIN_LCD_CS,
        .pclk_hz = LCD_SPI_HZ,
        .lcd_cmd_bits = 8,
        .lcd_param_bits = 8,
        .spi_mode = 0,
        .trans_queue_depth = 10,
        .on_color_trans_done = on_color_trans_done,
        .user_ctx = disp,
    };
    ESP_ERROR_CHECK(esp_lcd_new_panel_io_spi((esp_lcd_spi_bus_handle_t)LCD_HOST, &io_cfg, &io));

    esp_lcd_panel_dev_config_t panel_cfg = {
        .reset_gpio_num = PIN_LCD_RST,
        .rgb_ele_order = LCD_RGB_ELEMENT_ORDER_BGR,
        .bits_per_pixel = 16,
    };
    ESP_ERROR_CHECK(esp_lcd_new_panel_ili9341(io, &panel_cfg, &s_panel));
    ESP_ERROR_CHECK(esp_lcd_panel_reset(s_panel));
    ESP_ERROR_CHECK(esp_lcd_panel_init(s_panel));
    ESP_ERROR_CHECK(esp_lcd_panel_swap_xy(s_panel, true));
    ESP_ERROR_CHECK(esp_lcd_panel_mirror(s_panel, LCD_MIRROR_X, LCD_MIRROR_Y));
    ESP_ERROR_CHECK(esp_lcd_panel_disp_on_off(s_panel, true));

    size_t buf_sz = LCD_H_RES * LVGL_BUF_LINES * sizeof(uint16_t);
    void *buf1 = heap_caps_malloc(buf_sz, MALLOC_CAP_DMA);
    void *buf2 = heap_caps_malloc(buf_sz, MALLOC_CAP_DMA);
    assert(buf1 && buf2);

    lv_tick_set_cb(tick_cb);
    lv_display_set_color_format(disp, LV_COLOR_FORMAT_RGB565);
    lv_display_set_buffers(disp, buf1, buf2, buf_sz, LV_DISPLAY_RENDER_MODE_PARTIAL);
    lv_display_set_flush_cb(disp, flush_cb);
    lv_display_set_default(disp);

    gpio_set_level(PIN_LCD_BL, LCD_BL_ON_LEVEL);
    ESP_LOGI(TAG, "ILI9341 %dx%d ready", LCD_H_RES, LCD_V_RES);
}

static void pin_label_topleft(lv_obj_t *obj, int32_t width)
{
    lv_obj_update_layout(obj);
    int32_t x = lv_obj_get_x(obj);
    int32_t y = lv_obj_get_y(obj);
    lv_obj_set_align(obj, LV_ALIGN_TOP_LEFT);
    lv_obj_set_pos(obj, x, y);
    if (width > 0)
    {
        lv_obj_set_width(obj, width);
    }
}

static lv_obj_t *s_energy_title;
static lv_obj_t *s_energy_value;
static lv_obj_t *s_alarm_label;

static void create_energy_labels(void)
{
    static lv_style_t style;
    lv_style_init(&style);
    lv_style_set_text_font(&style, &Font_1);
    lv_style_set_text_color(&style, lv_color_make(255, 255, 255));
    lv_style_set_text_align(&style, LV_TEXT_ALIGN_LEFT);

    s_energy_title = lv_label_create(GUI_Screen__Screen);
    lv_obj_add_style(s_energy_title, &style, LV_PART_MAIN | LV_STATE_DEFAULT);
    lv_label_set_text_static(s_energy_title, "Energy:");
    lv_obj_align_to(s_energy_title, GUI_Label__Screen__Label_2, LV_ALIGN_OUT_BOTTOM_LEFT, 0, 8);

    s_energy_value = lv_label_create(GUI_Screen__Screen);
    lv_obj_add_style(s_energy_value, &style, LV_PART_MAIN | LV_STATE_DEFAULT);
    lv_obj_set_width(s_energy_value, 120);
    lv_label_set_text_static(s_energy_value, "0.000 kWh");
    lv_obj_align_to(s_energy_value, GUI_Label__Screen__Label_5, LV_ALIGN_OUT_BOTTOM_LEFT, 0, 8);
}

static void ui_update(const measurement_data_t *d)
{
    char buf[24];
    snprintf(buf, sizeof(buf), "%.1f V", (double)d->voltage);
    lv_label_set_text(GUI_Label__Screen__Label_3, buf);
    snprintf(buf, sizeof(buf), "%.2f A", (double)d->current);
    lv_label_set_text(GUI_Label__Screen__Label_4, buf);
    snprintf(buf, sizeof(buf), "%.1f W", (double)d->active_power);
    lv_label_set_text(GUI_Label__Screen__Label_5, buf);
    snprintf(buf, sizeof(buf), "%.3f kWh", d->energy_wh / 1000.0);
    lv_label_set_text(s_energy_value, buf);
}

static void lvgl_task(void *arg)
{
    GUI_load();

    ESP_LOGI(TAG, "GUI loaded, free heap=%u", (unsigned)esp_get_free_heap_size());
    pin_label_topleft(GUI_Label__Screen__Label_3, 100);
    pin_label_topleft(GUI_Label__Screen__Label_4, 100);
    pin_label_topleft(GUI_Label__Screen__Label_5, 100);
    create_energy_labels();

    int64_t last_ui = 0;
    while (1)
    {
        int64_t now = esp_timer_get_time();
        if (now - last_ui >= 200000)
        {
            last_ui = now;
            measurement_data_t d;
            data_control_get(&d);
            ui_update(&d);
        }
        GUI_refresh();
        vTaskDelay(pdMS_TO_TICKS(10));
    }
}

esp_err_t display_start(void)
{
    BaseType_t ok = xTaskCreate(lvgl_task, "lvgl", 8192, NULL, 4, NULL);
    return ok == pdPASS ? ESP_OK : ESP_ERR_NO_MEM;
}
