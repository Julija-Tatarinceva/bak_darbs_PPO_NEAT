import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.ticker as ticker

neat_files = ['NEAT_2.csv', 'NEAT_4.csv', 'NEAT_6.csv']
dfs_clipped = []

for f in neat_files:
    df = pd.read_csv(f)
    
    offset = df_clipped.iloc[0]['FixedUpdateCount']
    df_clipped['Steps'] = (df_clipped['FixedUpdateCount'] - offset) / 5
    df_clipped['Reward'] = df_clipped['Pieces'] * 1.0 - df_clipped['WallHits'] * 0.2
    df_clipped['MA10'] = df_clipped['Reward'].rolling(window=10).mean() # Окно меньше, так как данных мало
    dfs_clipped.append(df_clipped)

# Визуализация
plt.figure(figsize=(12, 6), dpi=150)
colors = ['#d62728', '#1f77b4', '#2ca02c'] # Красный, Синий, Зеленый

for i, df in enumerate(dfs_clipped):
    label = f"{i+1}. sērija"
    # Рисуем точки (сырые данные) и линию (среднее)
    plt.scatter(df['Episode'], df['Reward'], color=colors[i], alpha=0.2, s=10)
    plt.plot(df['Episode'], df['MA10'], color=colors[i], label=label, linewidth=2)

plt.xlabel('Epizožu skaits (pirmās 100)', fontsize=12)
plt.ylabel('Atalgojums', fontsize=12)
plt.title('NEAT algoritma apmācības pirmās 100 epizodes', fontsize=14)
plt.ylim(-2, 16)
plt.xlim(0, 100)

plt.grid(True, linestyle='--', alpha=0.6)
plt.legend(loc='lower right')
plt.tight_layout()
plt.savefig('neat_100_episodes.png')
plt.show()

# Пересчет таблицы для первых 100 эпизодов
stats_100 = []
for i, df in enumerate(dfs_clipped):
    avg_reward = round(df['Reward'].mean(), 2)
    max_reward = round(df['Reward'].max(), 2)
    avg_hits = round(df['WallHits'].mean(), 2)
    
    stats_100.append({
        "Sērija": f"{i+1}. sērija",
        "Vidējais atalgojums": avg_reward,
        "Maks. atalgojums": max_reward,
        "Vid. sadursmes": avg_hits
    })

print(pd.DataFrame(stats_100).to_string(index=False))