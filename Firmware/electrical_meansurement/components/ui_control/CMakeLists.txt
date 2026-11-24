idf_component_register(
    SRCS "ui_control.c"
    INCLUDE_DIRS "."
    REQUIRES data_screen esp_lcd esp_timer nvs_flash lvgl__lvgl espressif__esp_lcd_ili9341 ui
)