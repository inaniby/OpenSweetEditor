#include <catch2/catch_amalgamated.hpp>
#include "editor_core.h"
#include "test_measurer.h"

using namespace NS_SWEETEDITOR;

TEST_CASE("EditorCore buildRenderModel exposes normalized selection handles") {
  EditorOptions options;
  EditorCore editor(makeShared<FixedWidthTextMeasurer>(10.0f), options);

  editor.loadDocument(makeShared<LineArrayDocument>("abcdef"));
  editor.setViewport({320, 120});
  editor.setSelection({{0, 5}, {0, 2}});

  EditorRenderModel model;
  editor.buildRenderModel(model);

  REQUIRE_FALSE(model.selection_rects.empty());
  CHECK_FALSE(model.cursor.visible);
  CHECK(model.selection_start_handle.visible);
  CHECK(model.selection_end_handle.visible);
  CHECK(model.selection_start_handle.position.x <= model.selection_end_handle.position.x);
}

TEST_CASE("EditorCore buildRenderModel exposes active composition decoration") {
  EditorOptions options;
  EditorCore editor(makeShared<FixedWidthTextMeasurer>(10.0f), options);

  editor.loadDocument(makeShared<LineArrayDocument>("ab"));
  editor.setViewport({320, 120});
  editor.setCompositionEnabled(true);
  editor.setCursorPosition({0, 1});
  editor.compositionStart();
  editor.compositionUpdate("xy");

  EditorRenderModel model;
  editor.buildRenderModel(model);

  REQUIRE(model.composition_decoration.active);
  CHECK(model.composition_decoration.rect.width > 0.0f);
  CHECK(model.composition_decoration.rect.height > 0.0f);
  CHECK(model.composition_decoration.rect.origin.x == Catch::Approx(editor.getPositionScreenRect({0, 1}).x));
}

TEST_CASE("EditorCore buildRenderModel emits linked editing rectangles for snippet tab stops") {
  EditorOptions options;
  EditorCore editor(makeShared<FixedWidthTextMeasurer>(10.0f), options);

  editor.loadDocument(makeShared<LineArrayDocument>(""));
  editor.setViewport({320, 120});
  REQUIRE(editor.insertSnippet("${1:foo}-${2:bar}-$0").changed);

  EditorRenderModel model;
  editor.buildRenderModel(model);

  REQUIRE(model.linked_editing_rects.size() == 2);
  size_t active_count = 0;
  size_t inactive_count = 0;
  for (const auto& rect : model.linked_editing_rects) {
    CHECK(rect.rect.width > 0.0f);
    CHECK(rect.rect.height > 0.0f);
    if (rect.is_active) active_count++;
    else inactive_count++;
  }
  CHECK(active_count == 1);
  CHECK(inactive_count == 1);
}

TEST_CASE("EditorCore buildRenderModel uses external bracket match positions when provided") {
  EditorOptions options;
  EditorCore editor(makeShared<FixedWidthTextMeasurer>(10.0f), options);

  editor.loadDocument(makeShared<LineArrayDocument>("a(b)c"));
  editor.setViewport({320, 120});
  editor.setMatchedBrackets({0, 1}, {0, 3});

  EditorRenderModel matched_model;
  editor.buildRenderModel(matched_model);

  REQUIRE(matched_model.bracket_highlight_rects.size() == 2);
  for (const auto& rect : matched_model.bracket_highlight_rects) {
    CHECK(rect.width > 0.0f);
    CHECK(rect.height > 0.0f);
  }

  editor.clearMatchedBrackets();
  EditorRenderModel cleared_model;
  editor.buildRenderModel(cleared_model);
  CHECK(cleared_model.bracket_highlight_rects.empty());
}
