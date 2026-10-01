import type { PricingScheduleRule } from '../types';

export type BillingInput = {
  elapsedMinutes: number;
  ratePerHour: number;
  buffetAmount?: number;
  freeMinutes?: number;
  discountPercent?: number;
  minimumCharge?: number;
  roundingStep?: number;
};

export type BillingBreakdown = {
  billableMinutes: number;
  timeAmount: number;
  buffetAmount: number;
  rawAmount: number;
  discountAmount: number;
  discountedAmount: number;
  roundedAmount: number;
  finalAmount: number;
};

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

function positiveInt(value: number | undefined, fallback = 0) {
  return Number.isFinite(value) ? Math.max(0, Math.round(value as number)) : fallback;
}

export function calculateBilling(input: BillingInput): BillingBreakdown {
  const elapsedMinutes = Math.max(0, Number(input.elapsedMinutes) || 0);
  const freeMinutes = Math.min(elapsedMinutes, positiveInt(input.freeMinutes));
  const billableMinutes = Math.max(0, elapsedMinutes - freeMinutes);
  const rawTime = Math.max(0, (Number(input.ratePerHour) || 0) * billableMinutes / 60);
  const minimumCharge = positiveInt(input.minimumCharge);
  const timeAmount = Math.max(Math.round(rawTime), billableMinutes > 0 ? minimumCharge : 0);
  const buffetAmount = positiveInt(input.buffetAmount);
  const rawAmount = timeAmount + buffetAmount;
  const discountPercent = clamp(Number(input.discountPercent) || 0, 0, 100);
  const discountAmount = Math.min(rawAmount, Math.round(rawAmount * discountPercent / 100));
  const discountedAmount = Math.max(0, rawAmount - discountAmount);
  const roundingStep = positiveInt(input.roundingStep, 0);
  const roundedAmount = roundingStep > 0 ? Math.round(discountedAmount / roundingStep) * roundingStep : discountedAmount;
  return { billableMinutes, timeAmount, buffetAmount, rawAmount, discountAmount, discountedAmount, roundedAmount, finalAmount: roundedAmount };
}

function minuteOfDay(date: Date) {
  return date.getHours() * 60 + date.getMinutes();
}

function matchesSchedule(rule: PricingScheduleRule, date: Date) {
  if (rule.active === false || !rule.weekdays.length) return false;
  if (!rule.weekdays.includes(date.getDay())) return false;
  const minute = minuteOfDay(date);
  if (rule.startMinute === rule.endMinute) return true;
  if (rule.startMinute < rule.endMinute) return minute >= rule.startMinute && minute < rule.endMinute;
  return minute >= rule.startMinute || minute < rule.endMinute;
}

export function resolvePricingRate(baseRate: number, schedule: PricingScheduleRule[] | undefined, at = new Date()) {
  const candidates = (schedule ?? []).filter(rule => matchesSchedule(rule, at)).sort((a, b) => (b.priority ?? 0) - (a.priority ?? 0));
  return candidates[0]?.pricePerHour ?? baseRate;
}
