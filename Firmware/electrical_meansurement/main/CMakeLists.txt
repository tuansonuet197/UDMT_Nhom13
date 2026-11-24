idf_component_register(SRCS "electrical_meansurement.c"
                    INCLUDE_DIRS "."
                    REQUIRES ui driver esp_timer nvs_flash
                             lvgl__lvgl espressif__esp_lcd_ili9341 data_pc ui_control)
