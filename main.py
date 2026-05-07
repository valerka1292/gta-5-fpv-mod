import os

def collect_csharp_code(source_dir, output_file):
    # Убеждаемся, что путь к исходной директории существует
    if not os.path.exists(source_dir):
        print(f"Ошибка: Директория {source_dir} не найдена.")
        return

    try:
        with open(output_file, 'w', encoding='utf-8') as outfile:
            # Рекурсивный обход всех папок в src
            for root, _, files in os.walk(source_dir):
                for file in files:
                    if file.endswith('.cs'):
                        file_path = os.path.join(root, file)
                        
                        # Записываем путь к файлу
                        outfile.write(f"{file_path}\n")
                        
                        # Читаем содержимое .cs файла
                        try:
                            with open(file_path, 'r', encoding='utf-8') as infile:
                                content = infile.read()
                                outfile.write(content)
                        except Exception as e:
                            outfile.write(f"[Ошибка чтения файла: {e}]")
                        
                        # Добавляем перенос строки между файлами для разделения
                        outfile.write("\n\n")
                        
        print(f"Готово! Весь код сохранен в {output_file}")
    
    except Exception as e:
        print(f"Произошла ошибка при записи: {e}")

# Настройки путей
src_path = r'C:\Users\Administrator\Desktop\projects\gta-5-fpv-mod\src'
output_path = './code.txt'

if __name__ == "__main__":
    collect_csharp_code(src_path, output_path)