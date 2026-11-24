idf_component_register(
    SRCS "data_screen.c"
    INCLUDE_DIRS "."
    REQUIRES driver esp_timer nvs_flash
)
