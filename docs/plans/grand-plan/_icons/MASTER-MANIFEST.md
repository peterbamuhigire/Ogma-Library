# Master Icon Manifest — consolidated buy-list

> Aggregates every phase `icons.md`. **Style:** flat full-color (D-001).
> **Source:** Flaticon, SVG master + PNG @1x/2x/3x (D-004/D-005).
> **Licence:** Flaticon Premium (no-attribution) for store distribution.
> Fill the `Flaticon asset` column as each icon is chosen, then flip its
> per-phase status to `✅ premium wired`.

**Total unique icon keys specced so far: ~299** (Phase 19 adds 16; some keys are shared/reused across phases).

## How to procure on Flaticon (the workflow)

1. **Pick ONE flat full-color family first.** On Flaticon, find a single
   author/pack whose flat full-color style covers books, shelves, reading,
   search, AI, settings, and people/school. Coherence (one grid/stroke/corner
   radius) matters more than picking the "perfect" individual icon. Lock that
   pack as the house style in Phase 03.
2. **Create one Flaticon collection per phase** named `ogma-phaseNN-<topic>`
   (e.g. `ogma-phase03-core`, `ogma-phase06-catalogue`). Add the chosen asset
   for each `icon_key` listed below to that collection.
3. **Confirm Premium licence** (no attribution, commercial + store
   redistribution) — required because Ogma ships on the Mac App Store and
   Windows Store. If on free tier, an in-app Flaticon credit screen is mandatory.
4. **Download SVG + PNG**; export PNG at @1x/2x/3x for 16/24/32/48 px. Place at
   `OgmaLibrary.App/Assets/icons/<category>/<icon_key>.svg` (+ `@Nx.png`).
5. **Record the mapping** here: fill `icon_key → Flaticon asset URL/ID` so the
   purchase is auditable and re-downloadable, and flip the phase `icons.md`
   status from `⬜ to procure` → `✅ premium wired`.
6. **Batch by part** to save effort: Design system (03) first, then Core Library
   (05-07), then Reader/Search (08-11), then Intelligence (12-13), then Signature
   (14-15), then Classroom (16-18), then the rest. Earliest needed: **Phase 03 +
   06** (the design system and the catalogue are the most icon-dense and gate the
   look of the whole app).

## Per-phase counts (recommended procurement batches)

| Phase | Surface | Unique icon keys | Suggested Flaticon collection |
| --- | --- | --- | --- |
| 03 | Design system / global chrome | 34 | `ogma-phase03` |
| 05 | Scan & health | 9 | `ogma-phase05` |
| 06 | Catalogue browsing | 42 | `ogma-phase06` |
| 07 | Metadata & health dashboard | 28 | `ogma-phase07` |
| 08 | Reader core | 23 | `ogma-phase08` |
| 09 | Annotations & memory | 22 | `ogma-phase09` |
| 10 | Search & index manager | 18 | `ogma-phase10` |
| 11 | Semantic search | 16 | `ogma-phase11` |
| 12 | AI gateway & Privacy Center | 17 | `ogma-phase12` |
| 13 | AI advisor & plans | 14 | `ogma-phase13` |
| 14 | 3D bookshelf | 11 | `ogma-phase14` |
| 15 | OCR & power tools | 14 | `ogma-phase15` |
| 16 | LAN host | 8 | `ogma-phase16` |
| 17 | Client/classroom | 9 | `ogma-phase17` |
| 18 | School admin & managed AI | 13 | `ogma-phase18` |
| 19 | Security & compliance | 19 | `ogma-phase19` |
| 20 | Performance/reliability | 7 | `ogma-phase20` |
| 21 | A11y/i18n (verification) | 0 | `ogma-phase21` |
| 22 | Packaging/stores | 2 | `ogma-phase22` |
| 23 | Launch/SDK | 5 | `ogma-phase23` |

## All icon keys by phase

### Phase 03

- `ic_ai_advisor`
- `ic_ai_disable`
- `ic_ai_privacy`
- `ic_app_logo`
- `ic_cat_filter`
- `ic_cat_shelf`
- `ic_cat_shelf_new`
- `ic_cat_sort`
- `ic_cat_view_directory`
- `ic_cat_view_grid`
- `ic_cat_view_list`
- `ic_cat_view_shelf3d`
- `ic_close`
- `ic_keyboard_shortcut`
- `ic_lib_folder_open`
- `ic_lib_health`
- `ic_lib_preferences`
- `ic_lib_scan`
- `ic_read_bookmark`
- `ic_read_fullscreen`
- `ic_read_open`
- `ic_read_zoom_in`
- `ic_read_zoom_out`
- `ic_search`
- `ic_search_fulltext`
- `ic_search_metadata`
- `ic_set_about`
- `ic_set_appearance`
- `ic_set_language`
- `ic_set_updates`
- `ic_settings`
- `ic_status_available`
- `ic_status_loading`
- `ic_status_unavailable`

### Phase 05

- `ic_file_missing`
- `ic_health_ok`
- `ic_health_warning`
- `ic_retry`
- `ic_scan_cancel`
- `ic_scan_complete`
- `ic_scan_error`
- `ic_scan_library`
- `ic_scan_progress`

### Phase 06

- `ic_3d_view`
- `ic_available`
- `ic_book_no_cover`
- `ic_bulk_deselect`
- `ic_bulk_edit`
- `ic_bulk_select_all`
- `ic_close_panel`
- `ic_directory_view`
- `ic_edit_inline`
- `ic_empty_filter`
- `ic_empty_library`
- `ic_empty_shelf`
- `ic_enrich`
- `ic_field_group_ai`
- `ic_field_group_biblio`
- `ic_field_group_enrichment`
- `ic_field_group_file`
- `ic_field_group_reading`
- `ic_filter`
- `ic_filter_clear`
- `ic_folder`
- `ic_grid_view`
- `ic_list_view`
- `ic_open_reader`
- `ic_preview`
- `ic_provenance`
- `ic_rating_star`
- `ic_rating_star_empty`
- `ic_settings`
- `ic_shelf`
- `ic_shelf_add`
- `ic_shelf_delete`
- `ic_shelf_drag`
- `ic_shelf_rename`
- `ic_shelf_smart`
- `ic_sort_asc`
- `ic_sort_desc`
- `ic_tag`
- `ic_tag_add`
- `ic_tag_remove`
- `ic_unavailable`
- `ic_undo`

### Phase 07

- `ic_accept_all`
- `ic_accept_field`
- `ic_batch_cancel`
- `ic_batch_pause`
- `ic_batch_resume`
- `ic_confidence_high`
- `ic_confidence_low`
- `ic_confidence_medium`
- `ic_duplicate`
- `ic_enrich`
- `ic_enrich_batch`
- `ic_health_all_clear`
- `ic_health_dashboard`
- `ic_isbn`
- `ic_isbn_detected`
- `ic_isbn_missing`
- `ic_missing_cover`
- `ic_missing_isbn`
- `ic_provider_google_books`
- `ic_provider_open_library`
- `ic_provider_user_override`
- `ic_quality_score`
- `ic_reject_field`
- `ic_writeback`
- `ic_writeback_backup`
- `ic_writeback_diff`
- `ic_writeback_failed`
- `ic_writeback_success`

### Phase 08

- `ic_display_continuous`
- `ic_display_single`
- `ic_display_two_page`
- `ic_reader_first_page`
- `ic_reader_fullscreen_enter`
- `ic_reader_fullscreen_exit`
- `ic_reader_jump_page`
- `ic_reader_last_page`
- `ic_reader_nav_back`
- `ic_reader_nav_forward`
- `ic_reader_next_page`
- `ic_reader_no_text_layer`
- `ic_reader_page_count`
- `ic_reader_prev_page`
- `ic_reader_search`
- `ic_reader_search_close`
- `ic_reader_search_next`
- `ic_reader_search_prev`
- `ic_zoom_fit_page`
- `ic_zoom_fit_width`
- `ic_zoom_fixed`
- `ic_zoom_in`
- `ic_zoom_out`

### Phase 09

- `ic_annotation_delete`
- `ic_annotation_highlight`
- `ic_annotation_highlight_color`
- `ic_annotation_note`
- `ic_annotation_note_anchor`
- `ic_annotation_panel`
- `ic_bookmark_add`
- `ic_bookmark_item`
- `ic_bookmark_panel`
- `ic_bookmark_remove`
- `ic_bookmark_rename`
- `ic_citation_capture`
- `ic_citation_copy`
- `ic_citation_export`
- `ic_layer_add`
- `ic_layer_delete`
- `ic_layer_hidden`
- `ic_layer_merge`
- `ic_layer_panel`
- `ic_layer_visible`
- `ic_reading_memory`
- `ic_reading_memory_disposition`

### Phase 10

- `ic_filter_chip_description`
- `ic_filter_chip_note`
- `ic_filter_chip_page`
- `ic_filter_chip_tag`
- `ic_filter_chip_toc`
- `ic_index_manager`
- `ic_index_rebuild`
- `ic_index_rebuild_cancel`
- `ic_index_size`
- `ic_index_status_failed`
- `ic_index_status_indexed`
- `ic_index_status_indexing`
- `ic_index_status_pending_ocr`
- `ic_search_clear`
- `ic_search_filter`
- `ic_search_global`
- `ic_search_no_results`
- `ic_search_result_book`

### Phase 11

- `ic_confidence_high`
- `ic_confidence_low`
- `ic_confidence_medium`
- `ic_embedding_erase`
- `ic_embedding_generating`
- `ic_match_author`
- `ic_match_description`
- `ic_match_note`
- `ic_match_semantic`
- `ic_match_tag`
- `ic_match_text_page`
- `ic_match_title`
- `ic_match_toc`
- `ic_ranking_hybrid`
- `ic_search_semantic`
- `ic_search_semantic_unavailable`

### Phase 12

- `ic_ai_audit`
- `ic_ai_consent`
- `ic_ai_cost`
- `ic_ai_delete_embeddings`
- `ic_ai_delete_history`
- `ic_ai_disable`
- `ic_ai_export_audit`
- `ic_ai_key`
- `ic_ai_no_training`
- `ic_ai_payload_preview`
- `ic_ai_provider_anthropic`
- `ic_ai_provider_ollama`
- `ic_ai_provider_openai`
- `ic_ai_tier_content`
- `ic_ai_tier_local`
- `ic_ai_tier_metadata`
- `ic_ai_tier_offline`

### Phase 13

- `ic_ai_answer_cite`
- `ic_ai_checkpoint`
- `ic_ai_confidence_high`
- `ic_ai_confidence_low`
- `ic_ai_confidence_medium`
- `ic_ai_confidence_very_high`
- `ic_ai_difficulty_advanced`
- `ic_ai_difficulty_intermediate`
- `ic_ai_difficulty_intro`
- `ic_ai_explain`
- `ic_ai_plan_step`
- `ic_ai_provenance`
- `ic_ai_reading_plan`
- `ic_ai_recommend`

### Phase 14

- `ic_shelf3d_book_focused`
- `ic_shelf3d_camera_orbit`
- `ic_shelf3d_camera_reset`
- `ic_shelf3d_camera_zoom`
- `ic_shelf3d_layout_grid3d`
- `ic_shelf3d_layout_shelf`
- `ic_shelf3d_loading`
- `ic_shelf3d_theme_dark`
- `ic_shelf3d_theme_light`
- `ic_shelf3d_toggle`
- `ic_shelf3d_unavailable`

### Phase 15

- `ic_batch_enrichment`
- `ic_batch_export_failed`
- `ic_batch_pause`
- `ic_batch_resume`
- `ic_ocr_active`
- `ic_ocr_completed`
- `ic_ocr_derived`
- `ic_ocr_failed`
- `ic_ocr_paused`
- `ic_ocr_run`
- `ic_reader_lock_open`
- `ic_reader_locked`
- `ic_reader_split`
- `ic_smartshelf_perf`

### Phase 16

- `ic_certificate`
- `ic_clients_connected`
- `ic_host_sharing`
- `ic_host_start`
- `ic_host_stop`
- `ic_network_lan`
- `ic_publish_folder`
- `ic_qr_fingerprint`

### Phase 17

- `ic_connect_to_library`
- `ic_host_sharing`
- `ic_mode_classroom`
- `ic_mode_standalone`
- `ic_offline`
- `ic_profile_guest`
- `ic_profile_student`
- `ic_profile_teacher`
- `ic_sync`

### Phase 18

- `ic_admin_console`
- `ic_ai_key`
- `ic_audit_log`
- `ic_certificate`
- `ic_curate_shelf`
- `ic_dpia_shield`
- `ic_enroll_profile`
- `ic_moderate_ai`
- `ic_permissions_roles`
- `ic_publish_folder`
- `ic_publish_folder_admin`
- `ic_quota`
- `ic_usage_chart`

### Phase 19

- `ic_audit_export`
- `ic_audit_trail`
- `ic_consent_region`
- `ic_credential_store`
- `ic_dpia`
- `ic_encryption_at_rest`
- `ic_encryption_off`
- `ic_key_remove`
- `ic_minor_data`
- `ic_no_training`
- `ic_path_guard`
- `ic_payload_preview`
- `ic_privacy_tier_`
- `ic_redaction`
- `ic_security_center`
- `ic_signed_failed`
- `ic_signed_verified`
- `ic_threat_model`
- `ic_untrusted_pdf`

### Phase 20

- `ic_diagnostics_panel`
- `ic_perf_meter_`
- `ic_perf_meter_fail`
- `ic_perf_meter_ok`
- `ic_perf_meter_warn`
- `ic_telemetry_off`
- `ic_telemetry_opt_in`

### Phase 22

- `ic_lan_host_disabled`
- `ic_settings_info`

### Phase 23

- `ic_import_calibre`
- `ic_import_goodreads`
- `ic_import_zotero`
- `ic_mcp_server`
- `ic_mcp_server_active`

