Based on https://github.com/rp-an-87/LootValue which is a fork of the original https://github.com/IhanaMies/LootValue

Very big thanks to IhanaMies

I don't intend to maintain this, as i made this to fit my own gameplay, but it is 100% functional

Improved features include:

- Improved tooltip UI
  - More messages, explainations, safe guards 
- Selling multiple items at once
  - Both items that have stacks (such as ammo) and multiple single items of the same
- Improved logic and safeguards to provide stability
- Improved flea market price fetching to prevent items from not being sold
- Price based on durability left for most items that have durability
- More accurate trader price checking (works for most items except armored rigs the last time I tested)
- Imrpoved configurable sales conditions
  - Whether the item is too damaged for the flea merkt;
  - Whether the item is missing vital parts (for weapons);
  - Whether the item flea market profit is below a threshold;
    - This actually takes in consideration total sale value not individual items  

Total refactor of code:
- Improved code readability (for those who would like to look at the code)
