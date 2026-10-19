<script setup lang="ts">
import type { CompletionContext } from "@codemirror/autocomplete";
import { javascript, javascriptLanguage } from "@codemirror/lang-javascript";
import { oneDark } from "@codemirror/theme-one-dark";
import { EditorView, basicSetup } from "codemirror";
import { onBeforeUnmount, onMounted, ref, watch } from "vue";
import { completionsFor } from "@/game/scriptCompletions";

/**
 * A code editor (CodeMirror) for a perk script: JavaScript, with highlighting, line numbers, brackets that close themselves, and
 * suggestions for what a script can use (snake., room., the keys a function may return, the functions themselves).
 */
const props = defineProps<{ modelValue: string; label?: string }>();
const emit = defineEmits<{ (event: "update:modelValue", value: string): void }>();

const host = ref<HTMLElement | null>(null);

function suggestScriptApi(context: CompletionContext) {
  const found = completionsFor(context.state.sliceDoc(0, context.pos));
  if (!found) return null;
  return { from: found.from, options: found.options, validFor: /^\w*$/ };
}
let view: EditorView | null = null;

onMounted(() => {
  if (!host.value) return;
  view = new EditorView({
    parent: host.value,
    doc: props.modelValue,
    extensions: [
      basicSetup,
      javascript(),
      javascriptLanguage.data.of({ autocomplete: suggestScriptApi }),
      oneDark,
      EditorView.contentAttributes.of({ "aria-label": props.label ?? "Script" }),
      EditorView.updateListener.of((update) => {
        if (update.docChanged) emit("update:modelValue", update.state.doc.toString());
      }),
    ],
  });
});

// The text can also change from outside, for example when edits are undone.
watch(
  () => props.modelValue,
  (text) => {
    if (!view || text === view.state.doc.toString()) return;
    view.dispatch({ changes: { from: 0, to: view.state.doc.length, insert: text } });
  }
);

onBeforeUnmount(() => view?.destroy());
</script>

<template>
  <div ref="host" class="script-editor" />
</template>

<style scoped>
.script-editor {
  border: 1px solid var(--snake-line);
  font-size: 13px;
}
.script-editor :deep(.cm-editor) {
  max-height: 380px;
}
.script-editor :deep(.cm-scroller) {
  overflow: auto;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}
</style>
