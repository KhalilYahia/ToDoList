"use client";

import { Fragment, useState } from "react";
import {
  ArrowDown,
  ArrowUp,
  Plus,
  Trash2,
  Copy,
  Edit2,
  Check,
  Folder,
  FolderTree,
  HelpCircle,
  Sliders,
  Type,
  AlignLeft,
  ListOrdered,
  Info,
  X,
} from "lucide-react";

import { Button, Field, Input, Select, Textarea } from "@/components/ui/primitives";
import { enumCodes } from "@/lib/api/enums";

export interface ChecklistTreeItem {
  id?: string;
  title: string;
  description: string;
  isRequired: boolean;
  evidenceMode: string;
  itemType?: string;
  options?: string;
  mainBlockTitle?: string;
  subBlockTitle?: string;
}

interface TreeChecklistBuilderProps {
  items: ChecklistTreeItem[];
  onAppend: (item: ChecklistTreeItem) => void;
  onRemove: (index: number) => void;
  onMove: (from: number, to: number) => void;
  onUpdate: (index: number, item: ChecklistTreeItem) => void;
}

export function TreeChecklistBuilder({
  items,
  onAppend,
  onRemove,
  onMove,
  onUpdate,
}: TreeChecklistBuilderProps) {
  const [isTypeModalOpen, setIsTypeModalOpen] = useState(false);
  const [editingIndex, setEditingIndex] = useState<number | null>(null);

  function createEmptyItem(type = "SingleLineText", mainBlock = "", subBlock = ""): ChecklistTreeItem {
    return {
      title: "",
      description: "",
      isRequired: true,
      evidenceMode: "None",
      itemType: type,
      options: type === "MultipleChoice" ? "Option 1, Option 2, Option 3" : "",
      mainBlockTitle: mainBlock,
      subBlockTitle: subBlock,
    };
  }

  function handleAddItem(type: string) {
    const newItem = createEmptyItem(type);
    onAppend(newItem);
    setIsTypeModalOpen(false);
    setEditingIndex(items.length);
  }

  function handleAddMainBlock() {
    const blockTitle = prompt("Введите название блока (Main Block):") || "Новый блок";
    const newItem = createEmptyItem("Question", blockTitle, "");
    onAppend(newItem);
    setEditingIndex(items.length);
  }

  function handleAddSubBlock() {
    const subBlockTitle = prompt("Введите название подблока (Subblock):") || "Новый подблок";
    const lastMainBlock = [...items].reverse().find((i) => i.mainBlockTitle)?.mainBlockTitle || "Основной блок";
    const newItem = createEmptyItem("Question", lastMainBlock, subBlockTitle);
    onAppend(newItem);
    setEditingIndex(items.length);
  }

  function handleDuplicate(index: number) {
    const original = items[index];
    if (!original) return;
    const clone: ChecklistTreeItem = {
      ...original,
      id: undefined,
      title: `${original.title} (Копия)`,
    };
    onAppend(clone);
  }

  function getItemIcon(itemType?: string) {
    switch (itemType) {
      case "Question":
        return <HelpCircle className="size-4 text-sky-500 shrink-0" />;
      case "RatingSlider":
        return <Sliders className="size-4 text-amber-500 shrink-0" />;
      case "SingleLineText":
        return <Type className="size-4 text-emerald-500 shrink-0" />;
      case "MultiLineText":
        return <AlignLeft className="size-4 text-purple-500 shrink-0" />;
      case "MultipleChoice":
        return <ListOrdered className="size-4 text-rose-500 shrink-0" />;
      case "Instruction":
        return <Info className="size-4 text-blue-500 shrink-0" />;
      default:
        return <HelpCircle className="size-4 text-sky-500 shrink-0" />;
    }
  }

  return (
    <div className="grid gap-4">
      {/* ACTION BAR */}
      <div className="flex flex-wrap items-center justify-between gap-3 bg-surface-50 border border-ink-950/10 p-3 rounded-xl">
        <h3 className="font-black text-ink-900 text-base">Элементы и Блоки Чек-листа</h3>
        <div className="flex flex-wrap items-center gap-2">
          <Button type="button" size="sm" variant="secondary" onClick={handleAddMainBlock}>
            <Folder className="size-4 text-amber-500" /> + Добавить блок
          </Button>
          <Button type="button" size="sm" variant="secondary" onClick={handleAddSubBlock}>
            <FolderTree className="size-4 text-indigo-500" /> + Добавить подблок
          </Button>
          <Button type="button" size="sm" onClick={() => setIsTypeModalOpen(true)}>
            <Plus className="size-4" /> + Добавить пункт
          </Button>
        </div>
      </div>

      {/* ITEM TYPE SELECTION MODAL MATCHING SCREENSHOT */}
      {isTypeModalOpen ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
          <div className="w-full max-w-lg rounded-2xl bg-slate-800 text-white shadow-2xl overflow-hidden border border-slate-700">
            <div className="flex items-center justify-between border-b border-slate-700 px-6 py-4">
              <h3 className="text-sm font-bold tracking-wider uppercase text-slate-200">
                ВЫБЕРИТЕ ТИП ПУНКТА
              </h3>
              <button
                type="button"
                onClick={() => setIsTypeModalOpen(false)}
                className="text-slate-400 hover:text-white"
              >
                <X className="size-5" />
              </button>
            </div>
            <div className="grid gap-3 p-6 max-h-[80vh] overflow-y-auto">
              <button
                type="button"
                onClick={() => handleAddItem("Question")}
                className="flex items-start gap-4 rounded-xl border border-slate-700 bg-slate-900/60 p-4 text-left hover:bg-slate-700/50 transition-colors"
              >
                <HelpCircle className="size-6 text-sky-400 shrink-0 mt-0.5" />
                <div>
                  <h4 className="font-bold text-white">Вопрос</h4>
                  <p className="text-xs text-slate-400 mt-0.5">
                    Вопрос, который требует ответа Да/Нет
                  </p>
                </div>
              </button>

              <button
                type="button"
                onClick={() => handleAddItem("RatingSlider")}
                className="flex items-start gap-4 rounded-xl border border-slate-700 bg-slate-900/60 p-4 text-left hover:bg-slate-700/50 transition-colors"
              >
                <Sliders className="size-6 text-amber-400 shrink-0 mt-0.5" />
                <div>
                  <h4 className="font-bold text-white">Слайдер (оценка)</h4>
                  <p className="text-xs text-slate-400 mt-0.5">
                    Вопрос, который требует оценки в баллах ("Оцените от 0 до 10")
                  </p>
                </div>
              </button>

              <button
                type="button"
                onClick={() => handleAddItem("SingleLineText")}
                className="flex items-start gap-4 rounded-xl border border-slate-700 bg-slate-900/60 p-4 text-left hover:bg-slate-700/50 transition-colors"
              >
                <Type className="size-6 text-emerald-400 shrink-0 mt-0.5" />
                <div>
                  <h4 className="font-bold text-white">Строка для ввода</h4>
                  <p className="text-xs text-slate-400 mt-0.5">
                    Одна строчка для ввода ответа
                  </p>
                </div>
              </button>

              <button
                type="button"
                onClick={() => handleAddItem("MultiLineText")}
                className="flex items-start gap-4 rounded-xl border border-slate-700 bg-slate-900/60 p-4 text-left hover:bg-slate-700/50 transition-colors"
              >
                <AlignLeft className="size-6 text-purple-400 shrink-0 mt-0.5" />
                <div>
                  <h4 className="font-bold text-white">Текст</h4>
                  <p className="text-xs text-slate-400 mt-0.5">
                    Несколько строк для ввода (развернутый ответ)
                  </p>
                </div>
              </button>

              <button
                type="button"
                onClick={() => handleAddItem("MultipleChoice")}
                className="flex items-start gap-4 rounded-xl border border-slate-700 bg-slate-900/60 p-4 text-left hover:bg-slate-700/50 transition-colors"
              >
                <ListOrdered className="size-6 text-rose-400 shrink-0 mt-0.5" />
                <div>
                  <h4 className="font-bold text-white">Варианты ответа</h4>
                  <p className="text-xs text-slate-400 mt-0.5">
                    Вопрос, с настраиваемыми вариантами ответов
                  </p>
                </div>
              </button>

              <button
                type="button"
                onClick={() => handleAddItem("Instruction")}
                className="flex items-start gap-4 rounded-xl border border-slate-700 bg-slate-900/60 p-4 text-left hover:bg-slate-700/50 transition-colors"
              >
                <Info className="size-6 text-blue-400 shrink-0 mt-0.5" />
                <div>
                  <h4 className="font-bold text-white">Инструкция</h4>
                  <p className="text-xs text-slate-400 mt-0.5">
                    Описание способа достижения желаемого результата
                  </p>
                </div>
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {/* TREE TABLE MATCHING ATTACHED SCREENSHOT */}
      <div className="overflow-x-auto rounded-xl border border-ink-950/10 bg-white">
        <table className="w-full text-left text-sm border-collapse">
          <thead className="bg-surface-50 text-ink-500 uppercase text-xs font-bold border-b border-ink-950/10">
            <tr>
              <th className="p-3 ps-4">НАЗВАНИЕ</th>
              <th className="p-3 w-44 text-right pe-4">АКТИВНОСТЬ</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-ink-950/5">
            {items.map((item, index) => {
              const isMainBlockHeader =
                item.mainBlockTitle &&
                (index === 0 || items[index - 1]?.mainBlockTitle !== item.mainBlockTitle);
              const isSubBlockHeader =
                item.subBlockTitle &&
                (index === 0 || items[index - 1]?.subBlockTitle !== item.subBlockTitle);
              const isEditing = editingIndex === index;

              return (
                <Fragment key={index}>
                  {/* MAIN BLOCK HEADER ROW */}
                  {isMainBlockHeader ? (
                    <tr className="bg-slate-50/80 font-bold border-t border-b border-slate-200">
                      <td className="p-3 ps-4" colSpan={2}>
                        <div className="flex items-center gap-2">
                          <Folder className="size-5 text-amber-500 shrink-0" />
                          <span className="text-ink-900 text-base font-extrabold">
                            {item.mainBlockTitle}
                          </span>
                        </div>
                      </td>
                    </tr>
                  ) : null}

                  {/* SUBBLOCK HEADER ROW */}
                  {isSubBlockHeader ? (
                    <tr className="bg-slate-50/40 font-semibold border-b border-slate-100">
                      <td className="p-3 ps-8" colSpan={2}>
                        <div className="flex items-center gap-2">
                          <FolderTree className="size-4 text-indigo-500 shrink-0" />
                          <span className="text-ink-800 text-sm font-bold">
                            {item.subBlockTitle}
                          </span>
                        </div>
                      </td>
                    </tr>
                  ) : null}

                  {/* ITEM ROW (MATCHING USER SCREENSHOT) */}
                  <tr className="hover:bg-slate-50 transition-colors">
                    <td className={`p-3 align-middle ${item.subBlockTitle ? "ps-12" : item.mainBlockTitle ? "ps-8" : "ps-4"}`}>
                      <div className="flex items-center gap-3">
                        {/* MOVE ARROWS (↓ ↑) */}
                        <div className="flex items-center gap-0.5 text-slate-400 shrink-0">
                          <button
                            type="button"
                            disabled={index === 0}
                            onClick={() => onMove(index, index - 1)}
                            className="hover:text-slate-700 disabled:opacity-30"
                          >
                            <ArrowDown className="size-3.5" />
                          </button>
                          <button
                            type="button"
                            disabled={index === items.length - 1}
                            onClick={() => onMove(index, index + 1)}
                            className="hover:text-slate-700 disabled:opacity-30"
                          >
                            <ArrowUp className="size-3.5" />
                          </button>
                        </div>

                        {/* ITEM ICON IN CIRCLE */}
                        <div className="size-7 rounded-full bg-slate-100 flex items-center justify-center shrink-0 border border-slate-200">
                          {getItemIcon(item.itemType)}
                        </div>

                        {/* TITLE & DESCRIPTION */}
                        <div className="min-w-0 flex-1">
                          <p className="font-semibold text-ink-900 text-sm">
                            {item.title || <span className="text-slate-400 italic">Без названия (нажмите изм.)</span>}
                          </p>
                          {item.description ? (
                            <p className="text-slate-500 text-xs mt-0.5">{item.description}</p>
                          ) : null}
                        </div>
                      </div>
                    </td>

                    {/* ACTIVITY CHECKMARK & ACTION BUTTONS MATCHING SCREENSHOT */}
                    <td className="p-3 align-middle text-right pe-4">
                      <div className="flex items-center justify-end gap-3">
                        {/* GREEN CHECKMARK ICON */}
                        <Check className="size-5 text-emerald-500 shrink-0 font-bold" />

                        {/* ACTION BUTTONS (BLUE PENCIL, BLUE COPY, RED CROSS) */}
                        <div className="flex items-center gap-1 shrink-0">
                          {/* EDIT BUTTON (PENCIL) */}
                          <button
                            type="button"
                            onClick={() => setEditingIndex(isEditing ? null : index)}
                            className="size-7 rounded bg-sky-500 text-white flex items-center justify-center hover:bg-sky-600 shadow-xs transition-colors"
                            title="Изменить"
                          >
                            <Edit2 className="size-3.5" />
                          </button>

                          {/* COPY BUTTON (DUPLICATE) */}
                          <button
                            type="button"
                            onClick={() => handleDuplicate(index)}
                            className="size-7 rounded bg-sky-500 text-white flex items-center justify-center hover:bg-sky-600 shadow-xs transition-colors"
                            title="Дублировать"
                          >
                            <Copy className="size-3.5" />
                          </button>

                          {/* DELETE BUTTON (RED CROSS) */}
                          <button
                            type="button"
                            disabled={items.length === 1}
                            onClick={() => onRemove(index)}
                            className="size-7 rounded bg-rose-500 text-white flex items-center justify-center hover:bg-rose-600 shadow-xs transition-colors disabled:opacity-50"
                            title="Удалить"
                          >
                            <X className="size-3.5" />
                          </button>
                        </div>
                      </div>
                    </td>
                  </tr>

                  {/* INLINE EDIT PANEL WHEN EDITING CLICKED */}
                  {isEditing ? (
                    <tr className="bg-sky-50/50 border-b border-sky-100">
                      <td colSpan={2} className="p-4">
                        <div className="grid gap-3 max-w-3xl bg-white p-4 rounded-xl border border-sky-200 shadow-sm">
                          <h4 className="font-bold text-xs uppercase tracking-wider text-sky-900">
                            Редактирование пункта #{index + 1}
                          </h4>
                          <div className="grid gap-3 sm:grid-cols-2">
                            <Field label="Основной блок (Main Block)">
                              <Input
                                value={item.mainBlockTitle || ""}
                                onChange={(e) =>
                                  onUpdate(index, { ...item, mainBlockTitle: e.target.value })
                                }
                                placeholder="Основной блок..."
                              />
                            </Field>
                            <Field label="Подблок (Subblock)">
                              <Input
                                value={item.subBlockTitle || ""}
                                onChange={(e) =>
                                  onUpdate(index, { ...item, subBlockTitle: e.target.value })
                                }
                                placeholder="Подблок..."
                              />
                            </Field>
                          </div>
                          <Field label="Название пункта" required>
                            <Input
                              value={item.title}
                              onChange={(e) => onUpdate(index, { ...item, title: e.target.value })}
                              placeholder="Введите название пункта..."
                            />
                          </Field>
                          {item.itemType === "Question" ? (
                            <div className="text-xs text-sky-800 bg-sky-50 p-2.5 rounded-lg border border-sky-200 font-medium">
                              Ответ для вопроса: <strong>Да / Нет</strong> (пользователь выбирает флажок Yes/No, ввод текста заблокирован).
                            </div>
                          ) : item.itemType === "MultipleChoice" ? (
                            <Field label="Варианты ответа (через запятую)">
                              <Input
                                value={item.options || ""}
                                onChange={(e) =>
                                  onUpdate(index, { ...item, options: e.target.value })
                                }
                                placeholder="Отлично, Хорошо, Удовлетворительно"
                              />
                            </Field>
                          ) : null}
                          <Field label="Инструкции / Описание">
                            <Textarea
                              className="min-h-16 text-xs"
                              value={item.description || ""}
                              onChange={(e) =>
                                onUpdate(index, { ...item, description: e.target.value })
                              }
                              placeholder="Описание способа выполнения..."
                            />
                          </Field>
                          <div className="grid gap-3 sm:grid-cols-2">
                            <Field label="Тип пункта">
                              <Select
                                value={item.itemType || "SingleLineText"}
                                onChange={(e) =>
                                  onUpdate(index, { ...item, itemType: e.target.value })
                                }
                              >
                                {enumCodes.taskItemType.map((type) => (
                                  <option key={type} value={type}>
                                    {type}
                                  </option>
                                ))}
                              </Select>
                            </Field>
                            <Field label="Режим подтверждения">
                              <Select
                                value={item.evidenceMode}
                                onChange={(e) =>
                                  onUpdate(index, { ...item, evidenceMode: e.target.value })
                                }
                              >
                                {enumCodes.evidenceMode.map((mode) => (
                                  <option key={mode} value={mode}>
                                    {mode}
                                  </option>
                                ))}
                              </Select>
                            </Field>
                          </div>
                          <div className="flex items-center justify-between pt-2">
                            <label className="flex items-center gap-2 text-xs font-bold text-slate-700">
                              <input
                                type="checkbox"
                                checked={item.isRequired}
                                onChange={(e) =>
                                  onUpdate(index, { ...item, isRequired: e.target.checked })
                                }
                              />
                              Обязательный пункт
                            </label>
                            <Button
                              type="button"
                              size="sm"
                              onClick={() => setEditingIndex(null)}
                            >
                              Готово
                            </Button>
                          </div>
                        </div>
                      </td>
                    </tr>
                  ) : null}
                </Fragment>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
