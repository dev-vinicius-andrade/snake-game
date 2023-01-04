import { StringCase } from "@enums";
import { RendererElement, RendererNode, VNode } from "vue";
declare type VNodeRenderer = VNode<
  RendererNode,
  RendererElement,
  { [key: string]: any }
>;
declare type ILocaleText = {
  text?: string;
  key?: string;
  single?: boolean;
  multiple?: boolean;
  count?: string;
  params?: Record<string, unknown>;
  stringCases?: StringCase[];
  removeDoubleSpaces?: boolean;
};

export type { VNodeRenderer, ILocaleText };
