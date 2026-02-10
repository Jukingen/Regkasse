export interface Denomination {
  value: number;
  count: number;
  type: 'coin' | 'note';
  name: string;
}

export interface ChangeResult {
  totalChange: number;
  denominations: Denomination[];
  totalCount: number;
  isOptimal: boolean;
  alternativeOptions?: ChangeResult[];
}

export interface CashRegister {
  id: string;
  name: string;
  denominations: Denomination[];
  lastUpdated: Date;
}

export class ChangeOptimizer {
  private static instance: ChangeOptimizer;
  private availableDenominations: Denomination[] = [
    { value: 0.01, count: 100, type: 'coin', name: '1 Cent' },
    { value: 0.02, count: 100, type: 'coin', name: '2 Cent' },
    { value: 0.05, count: 100, type: 'coin', name: '5 Cent' },
    { value: 0.10, count: 100, type: 'coin', name: '10 Cent' },
    { value: 0.20, count: 100, type: 'coin', name: '20 Cent' },
    { value: 0.50, count: 100, type: 'coin', name: '50 Cent' },
    { value: 1.00, count: 100, type: 'coin', name: '1 Euro' },
    { value: 2.00, count: 100, type: 'coin', name: '2 Euro' },
    { value: 5.00, count: 50, type: 'note', name: '5 Euro' },
    { value: 10.00, count: 50, type: 'note', name: '10 Euro' },
    { value: 20.00, count: 50, type: 'note', name: '20 Euro' },
    { value: 50.00, count: 20, type: 'note', name: '50 Euro' },
    { value: 100.00, count: 10, type: 'note', name: '100 Euro' },
    { value: 200.00, count: 5, type: 'note', name: '200 Euro' },
    { value: 500.00, count: 2, type: 'note', name: '500 Euro' }
  ];

  public static getInstance(): ChangeOptimizer {
    if (!ChangeOptimizer.instance) {
      ChangeOptimizer.instance = new ChangeOptimizer();
    }
    return ChangeOptimizer.instance;
  }

  public setAvailableDenominations(denominations: Denomination[]): void {
    this.availableDenominations = denominations.sort((a, b) => b.value - a.value);
  }

  public getAvailableDenominations(): Denomination[] {
    return [...this.availableDenominations];
  }

  public calculateOptimalChange(amount: number, availableCash: Denomination[] = this.availableDenominations): ChangeResult {
    if (amount <= 0) {
      return {
        totalChange: 0,
        denominations: [],
        totalCount: 0,
        isOptimal: true
      };
    }

    const sortedDenominations = availableCash
      .filter(d => d.count > 0)
      .sort((a, b) => b.value - a.value);

    const result = this.greedyAlgorithm(amount, sortedDenominations);
    
    // Check if we can provide exact change
    if (Math.abs(result.totalChange - amount) < 0.01) {
      result.isOptimal = true;
    } else {
      result.isOptimal = false;
      // Try alternative combinations
      result.alternativeOptions = this.findAlternativeOptions(amount, sortedDenominations);
    }

    return result;
  }

  private greedyAlgorithm(amount: number, denominations: Denomination[]): ChangeResult {
    let remainingAmount = amount;
    const result: Denomination[] = [];
    let totalCount = 0;

    for (const denom of denominations) {
      if (remainingAmount <= 0) break;

      const maxCount = Math.min(
        Math.floor(remainingAmount / denom.value),
        denom.count
      );

      if (maxCount > 0) {
        result.push({
          value: denom.value,
          count: maxCount,
          type: denom.type,
          name: denom.name
        });
        remainingAmount -= maxCount * denom.value;
        totalCount += maxCount;
      }
    }

    return {
      totalChange: amount - remainingAmount,
      denominations: result,
      totalCount,
      isOptimal: remainingAmount < 0.01
    };
  }

  private findAlternativeOptions(amount: number, denominations: Denomination[]): ChangeResult[] {
    const alternatives: ChangeResult[] = [];
    
    // Try different combinations by reducing larger denominations
    for (let i = 0; i < denominations.length - 1; i++) {
      const modifiedDenominations = [...denominations];
      if (modifiedDenominations[i].count > 0) {
        modifiedDenominations[i].count--;
        const alternative = this.greedyAlgorithm(amount, modifiedDenominations);
        if (alternative.isOptimal && alternative.totalCount > 0) {
          alternatives.push(alternative);
        }
      }
    }

    // Sort by total count (prefer fewer pieces)
    return alternatives
      .sort((a, b) => a.totalCount - b.totalCount)
      .slice(0, 3); // Return top 3 alternatives
  }

  public suggestDenominationRestock(threshold: number = 10): Denomination[] {
    return this.availableDenominations
      .filter(d => d.count <= threshold)
      .map(d => ({
        ...d,
        suggestedRestock: Math.max(50, d.count * 2)
      }));
  }

  public calculateChangeEfficiency(changeResult: ChangeResult): {
    efficiency: number;
    averageValue: number;
    coinCount: number;
    noteCount: number;
  } {
    const { denominations, totalCount } = changeResult;
    
    if (totalCount === 0) {
      return {
        efficiency: 0,
        averageValue: 0,
        coinCount: 0,
        noteCount: 0
      };
    }

    const coinCount = denominations
      .filter(d => d.type === 'coin')
      .reduce((sum, d) => sum + d.count, 0);

    const noteCount = denominations
      .filter(d => d.type === 'note')
      .reduce((sum, d) => sum + d.count, 0);

    const averageValue = changeResult.totalChange / totalCount;
    
    // Efficiency based on average value and coin/note ratio
    const efficiency = Math.min(100, (averageValue / 10) * 100);

    return {
      efficiency,
      averageValue,
      coinCount,
      noteCount
    };
  }

  public validatePayment(amount: number, paymentAmount: number): {
    isValid: boolean;
    change: number;
    shortfall: number;
    message: string;
  } {
    const change = paymentAmount - amount;
    
    if (change < 0) {
      return {
        isValid: false,
        change: 0,
        shortfall: Math.abs(change),
        message: `Payment insufficient. Shortfall: €${Math.abs(change).toFixed(2)}`
      };
    }

    if (change === 0) {
      return {
        isValid: true,
        change: 0,
        shortfall: 0,
        message: 'Exact payment received'
      };
    }

    const changeResult = this.calculateOptimalChange(change);
    
    if (!changeResult.isOptimal) {
      return {
        isValid: false,
        change: changeResult.totalChange,
        shortfall: change - changeResult.totalChange,
        message: `Cannot provide exact change. Shortfall: €${(change - changeResult.totalChange).toFixed(2)}`
      };
    }

    return {
      isValid: true,
      change: changeResult.totalChange,
      shortfall: 0,
      message: `Change: €${changeResult.totalChange.toFixed(2)}`
    };
  }

  public getChangeBreakdown(changeResult: ChangeResult): {
    coins: Denomination[];
    notes: Denomination[];
    totalCoins: number;
    totalNotes: number;
    coinValue: number;
    noteValue: number;
  } {
    const coins = changeResult.denominations.filter(d => d.type === 'coin');
    const notes = changeResult.denominations.filter(d => d.type === 'note');

    const totalCoins = coins.reduce((sum, d) => sum + d.count, 0);
    const totalNotes = notes.reduce((sum, d) => sum + d.count, 0);
    const coinValue = coins.reduce((sum, d) => sum + (d.value * d.count), 0);
    const noteValue = notes.reduce((sum, d) => sum + (d.value * d.count), 0);

    return {
      coins,
      notes,
      totalCoins,
      totalNotes,
      coinValue,
      noteValue
    };
  }

  public simulateCashFlow(transactions: { amount: number; paymentAmount: number }[]): {
    totalChangeGiven: number;
    totalChangeReceived: number;
    netChange: number;
    denominationUsage: Map<number, number>;
    efficiency: number;
  } {
    let totalChangeGiven = 0;
    let totalChangeReceived = 0;
    const denominationUsage = new Map<number, number>();

    for (const transaction of transactions) {
      const change = transaction.paymentAmount - transaction.amount;
      
      if (change > 0) {
        totalChangeGiven += change;
        const changeResult = this.calculateOptimalChange(change);
        
        for (const denom of changeResult.denominations) {
          const currentUsage = denominationUsage.get(denom.value) || 0;
          denominationUsage.set(denom.value, currentUsage + denom.count);
        }
      } else if (change < 0) {
        totalChangeReceived += Math.abs(change);
      }
    }

    const netChange = totalChangeReceived - totalChangeGiven;
    const efficiency = totalChangeGiven > 0 ? (totalChangeGiven / (totalChangeGiven + totalChangeReceived)) * 100 : 0;

    return {
      totalChangeGiven,
      totalChangeReceived,
      netChange,
      denominationUsage,
      efficiency
    };
  }

  public optimizeDenominationStock(targetAmount: number, maxPieces: number = 100): Denomination[] {
    const optimized: Denomination[] = [];
    let remainingAmount = targetAmount;
    let remainingPieces = maxPieces;

    // Sort by value per piece efficiency
    const sortedDenominations = this.availableDenominations
      .sort((a, b) => (b.value / 1) - (a.value / 1));

    for (const denom of sortedDenominations) {
      if (remainingAmount <= 0 || remainingPieces <= 0) break;

      const maxCount = Math.min(
        Math.floor(remainingAmount / denom.value),
        remainingPieces,
        denom.count
      );

      if (maxCount > 0) {
        optimized.push({
          value: denom.value,
          count: maxCount,
          type: denom.type,
          name: denom.name
        });
        remainingAmount -= maxCount * denom.value;
        remainingPieces -= maxCount;
      }
    }

    return optimized;
  }
}

// Export singleton instance
export const changeOptimizer = ChangeOptimizer.getInstance(); 