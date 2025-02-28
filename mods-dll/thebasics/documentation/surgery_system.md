# The BASICs Surgery System

## Overview

The Surgery System is a comprehensive medical gameplay expansion for Vintage Story that allows players to perform surgical procedures on other players and entities. Inspired by Space Station 13's intricate medical gameplay, this system adds new dimensions to roleplay and survival.

## Key Features

- **Body Part System**: Entities are divided into multiple body parts (head, chest, arms, legs) that can be individually injured and treated.
- **Medical Conditions**: Injuries can cause bleeding and infections that progressively worsen if untreated.
- **Surgical Tools**: Use existing items as surgical tools (knives as scalpels, pliers as hemostats, etc.)
- **Step-by-Step Procedures**: Each surgery consists of multiple steps requiring different tools and skills.
- **Success/Failure System**: Surgeries can fail, potentially causing additional harm to the patient.

## Available Commands

- **/surgery [bodypart] [procedure]** - Begin a surgical procedure on a body part
- **/surgeryinfo** - List all available body parts and procedures
- **/surgeryinfo [bodypart]** - Show details about a specific body part
- **/surgeryinfo [procedure]** - Show details about a specific procedure
- **/bleed [rate]** - Admin command to cause bleeding in the targeted entity
- **/infect [level]** - Admin command to cause infection in the targeted entity
- **/damagebody <bodypart> [amount] [bleed] [infect]** - Admin command to damage a specific body part
- **/healbody <bodypart> [amount]** - Admin command to heal a specific body part

## Body Parts

The system tracks the following body parts:

- **Head** - Vital organ, injuries can be fatal
- **Chest** - Vital organ, injuries can be fatal
- **Left Arm** - Non-vital, but affects gameplay
- **Right Arm** - Non-vital, but affects gameplay
- **Left Leg** - Non-vital, but affects gameplay
- **Right Leg** - Non-vital, but affects gameplay

## Medical Conditions

Body parts can experience several conditions:

- **Bleeding** - Causes continuous health damage until treated
- **Infection** - Causes worsening health damage until treated
- **Broken** - Affects movement or actions depending on the part

## Surgical Procedures

The system includes these basic procedures:

- **Treat Wound** - Stops bleeding
- **Set Broken Bone** - Fixes broken limbs
- **Treat Infection** - Cures infections

## Performing Surgery

1. Target the patient (or no target for self-surgery)
2. Use the `/surgery [bodypart] [procedure]` command to start
3. Follow the prompts for each step, using the required tools
4. Right-click with the proper tool on the patient to perform each step
5. Complete all steps successfully to finish the procedure

## Surgical Tools

The following items are used as surgical tools:

- **Scalpel** - Any knife
- **Retractor** - Shears
- **Hemostat** - Pliers
- **Suture** - Flax twine
- **Bone Setter** - Hammer
- **Splint** - Sticks

## Configuration

The surgery system is highly configurable through the `surgery_config.json` file, allowing server owners to:

- Add or remove body parts
- Create new surgical procedures
- Adjust failure chances
- Customize healing amounts
- Define new surgical tools

## Tips for Successful Surgery

- Keep all necessary tools in your hotbar for quick access
- Perform surgery in well-lit areas
- Have healing items nearby in case of complications
- Practice on non-critical injuries first

## Future Expansions

The surgery system is designed for extensibility. Future updates may include:

- Visual body part injuries
- Surgical tables and specialized rooms
- Advanced tools and procedures
- Medical skill system
- Temperature and sterilization effects

---

*The surgery system was inspired by Space Station 13's medical gameplay systems. Many thanks to the SS13 community for the original concept.* 