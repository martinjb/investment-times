// app/models/models.ts
// TypeScript interfaces that mirror the C# DTOs returned by the API.
// Keeping these in one file makes it easy to spot if backend and frontend contracts drift.

export type AssetType = 0 | 1;     // 0 = Stock, 1 = Crypto (matches the C# enum)
export type TransactionType = 0 | 1; // 0 = Buy, 1 = Sell

export const AssetType = { Stock: 0 as AssetType, Crypto: 1 as AssetType };
export const TransactionType = { Buy: 0 as TransactionType, Sell: 1 as TransactionType };

export interface Transaction {
  id: number;
  symbol: string;
  assetType: AssetType;
  type: TransactionType;
  quantity: number;
  pricePerUnit: number;
  date: string;
}

export interface CreateTransaction {
  symbol: string;
  assetType: AssetType;
  type: TransactionType;
  quantity: number;
  pricePerUnit: number;
}

export interface Holding {
  symbol: string;
  assetType: AssetType;
  quantity: number;
  averageCost: number;
  currentPrice: number;
  marketValue: number;
  totalCost: number;
  unrealizedGain: number;
  unrealizedGainPercent: number;
}

export interface PortfolioSummary {
  totalCost: number;
  marketValue: number;
  unrealizedGain: number;
  unrealizedGainPercent: number;
  realizedGain: number;
  holdingCount: number;
}

export interface MarketIndicator {
  name: string;
  symbol: string;
  price: number;
  changePercent24h: number;
}

export interface MarketGroup {
  label: string;
  items: MarketIndicator[];
}

export interface NewsItem {
  title: string;
  source: string;
  url: string;
  publishedAt: string | null;
}
