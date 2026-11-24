idf_component_register(
    SRCS "assets/fonts/Font_1.c"
         "assets/images/upload_background_png.c"
         "behavior/animations.c"
         "behavior/events.c"
         "core/gui_core.c"
         "core/variables.c"
         "helpers/ui_helpers.c"
         "screens/Screen.c"
    INCLUDE_DIRS "."
    REQUIRES lvgl__lvgl
)
