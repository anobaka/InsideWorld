import type { ToastOptions } from "@react-stately/toast";

import { addToast, type ToastProps } from "@heroui/react";

// endContent is HeroUI's slot for an action beside the message. It was not in this
// wrapper's shape, so a toast that wanted to offer the user somewhere to go had to
// bypass the wrapper entirely.
type SimpleProps = {
  title: string;
  description?: string;
  endContent?: ToastProps["endContent"];
} & ToastOptions;

// 通用toast方法
function showToast(color: ToastProps["color"], titleOrProps: string | SimpleProps) {
  if (typeof titleOrProps === "string") {
    addToast({ color, title: titleOrProps });
  } else {
    addToast({ color, ...titleOrProps });
  }
}

const toast = {
  default: (titleOrProps: string | SimpleProps) => showToast("default", titleOrProps),
  primary: (titleOrProps: string | SimpleProps) => showToast("primary", titleOrProps),
  secondary: (titleOrProps: string | SimpleProps) => showToast("secondary", titleOrProps),
  success: (titleOrProps: string | SimpleProps) => showToast("success", titleOrProps),
  warning: (titleOrProps: string | SimpleProps) => showToast("warning", titleOrProps),
  danger: (titleOrProps: string | SimpleProps) => showToast("danger", titleOrProps),
  foreground: (titleOrProps: string | SimpleProps) => showToast("foreground", titleOrProps),
};

export default toast;
