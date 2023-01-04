

import  {ILocaleText}  from '@types';
import {StringCase} from "@enums"
import {Buffer} from 'buffer';
import { v4 as uuidv4} from 'uuid';
import i18n from '@plugins/i18n'
import _forEach from "lodash/forEach";
import _replace from "lodash/replace";

export function generateId():string{
  return uuidv4();
}
export function createObjectFromPropertyNames(propertyNames?:string[]):any{
  if(!propertyNames||propertyNames.length===0) return {};
  return Object.fromEntries(propertyNames.map((propertyName)=>[propertyName,undefined]))
}
export function getFileName(path:string, removeExtension = true) {
  const regex = /^.*[\\/]/;
  const directoryPath = path.match(regex);
  if(!directoryPath|| directoryPath.length === 0) return path; 
  const fileNameWithExtension = path.replace(directoryPath[0],"");
  if(!removeExtension) return fileNameWithExtension;
  const fileNameWithoutExtension = fileNameWithExtension.replace(/(.*)\.(.*?)$/, "$1")
  return fileNameWithoutExtension;
  
}

export function getText(text?: ILocaleText | string): string {
  if (typeof text === 'string') return text;
  const {t} = i18n.global;
  if (!text) return "";
  if (text.text) return text.text;
  if (!text.key) return "";
  if (text?.single && !text?.multiple ) return toStringCases(t(text.key, 1),text?.stringCases).trim();
  if (text?.multiple && !text?.single) return toStringCases(t(text.key, 2),text?.stringCases).trim();
  if (text?.count!==undefined && text?.count!==null && !text?.single && !text?.multiple ) return toStringCases(t(text.key, text?.count),text?.stringCases).trim();
  return toStringCases(t(text.key, text.params as Record<string,unknown> ,text.count as string ),text?.stringCases).trim();
}
export function removeDoubleSpaces(text?:string):string|null|undefined{
  if(!text) return text;
  return _replace(text, /\s\s+/g, ' ');
}
export const stringCaseHandlers={
  [StringCase.none]: (text:string)=>text,
  [StringCase.upper]: (text:string)=>text.toUpperCase(),
  [StringCase.lower]: (text:string)=>text.toLowerCase(),
  [StringCase.camel]: (text:string)=>toCammelCase(text),
  [StringCase.pascal]: (text:string)=>toPascalCase(text),
}
export function toStringCases(text:string,stringCases:StringCase[]=[StringCase.none]): string {
  let result = text;
  stringCases.forEach((stringCase)=>{
    result = stringCaseHandlers[stringCase](result);
  })
  return result;
}

export function toCammelCase(str:string) {
  return str.toLowerCase().replace(/[^a-zA-Z0-9]+(.)/g, (chr:string) => chr.toUpperCase());
}
export function toPascalCase(str:string) {
  return str.charAt(0).toUpperCase() + toCammelCase(str).slice(1);
}

export function convertRequestUrlWithTokenParams(url: string, params : {[key: string]: string} = {}) {
  _forEach(params, function(value : string, key : string) {
    url = _replace(url, key, value );
  });
  return url;
}

export function encodeBase64(text?:string)
{
  if(!text) return "";
  return Buffer.from(text).toString('base64');
}
export function decodeBase64(text?:string)
{
  if(!text) return "";
  return Buffer.from(text,'base64').toString('utf-8');
}

export function  nameof<T>(name: Extract<keyof T, string>): string{ return name}
